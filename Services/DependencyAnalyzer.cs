using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Services
{
	public class DependencyAnalyzer : IDependencyAnalyzer
	{
		private readonly ILogger<DependencyAnalyzer> _logger;
		private readonly IDataRepository _repository;

		public DependencyAnalyzer(ILogger<DependencyAnalyzer> logger, IDataRepository repository)
		{
			_logger = logger;
			_repository = repository;
		}

		public async Task<List<ProjectFile>> GetRelatedMigratedFilesAsync(ProjectFile currentFile, string projectId)
		{
			try
			{
				_logger.LogInformation($"Analyzing dependencies for file: {currentFile.FileName}");

				// Get all migrated files in the project
				var migratedFiles = await _repository.GetProjectFilesAsync(projectId, "migrated");

				var relatedFiles = new List<ProjectFile>();

				// Analyze different types of dependencies
				var dependencies = AnalyzeDependencies(currentFile);

				foreach (var migratedFile in migratedFiles)
				{
					if (IsRelatedFile(currentFile, migratedFile, dependencies))
					{
						relatedFiles.Add(migratedFile);
						_logger.LogInformation($"Found related migrated file: {migratedFile.FileName}");
					}
				}

				// Sort by relevance (most relevant first)
				return SortByRelevance(currentFile, relatedFiles);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing dependencies for {currentFile.FileName}");
				return new List<ProjectFile>();
			}
		}

		public async Task<List<string>> GetRelatedMigratedFileContentsAsync(ProjectFile currentFile, string projectId, MigrationProject project)
		{
			var relatedFiles = await GetRelatedMigratedFilesAsync(currentFile, projectId);
			var fileContents = new List<string>();

			_logger.LogInformation($"Processing {relatedFiles.Count} related files for {currentFile.FileName}");

			foreach (var relatedFile in relatedFiles.Take(5)) // Limit to 5 most relevant files to avoid prompt bloat
			{
				try
				{
					var content = await GetMigratedFileContentAsync(relatedFile, project);
					if (!string.IsNullOrEmpty(content))
					{
						fileContents.Add($"=== RELATED MIGRATED FILE: {relatedFile.FileName} ===\n{content}\n");
						_logger.LogInformation($"Added content from related file: {relatedFile.FileName}");
					}
					else
					{
						_logger.LogWarning($"No content found for related file: {relatedFile.FileName}");
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, $"Could not read migrated content for {relatedFile.FileName}");
				}
			}

			// If we have few related files, also include some recent similar files from the actual DAL/BAL projects
			if (fileContents.Count < 3)
			{
				_logger.LogInformation($"Only found {fileContents.Count} related files, looking for additional examples from project structure");
				var additionalExamples = await GetAdditionalProjectExamplesAsync(currentFile, project, fileContents.Count);
				fileContents.AddRange(additionalExamples);
			}

			_logger.LogInformation($"Total content sections prepared: {fileContents.Count}");
			return fileContents;
		}

		private async Task<List<string>> GetAdditionalProjectExamplesAsync(ProjectFile currentFile, MigrationProject project, int existingCount)
		{
			var examples = new List<string>();
			var maxAdditional = 3 - existingCount; // Up to 3 total examples

			if (maxAdditional <= 0) return examples;

			try
			{
				var recentFiles = new List<(string path, string type, DateTime modified)>();

				// Get recent files from DAL project
				if (!string.IsNullOrEmpty(project.DalProjectPath) && Directory.Exists(project.DalProjectPath))
				{
					var dalFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddDays(-7)) // Files from last week
						.Select(f => (f, "DAL", File.GetLastWriteTime(f)))
						.OrderByDescending(f => f.Item3);

					recentFiles.AddRange(dalFiles);
				}

				// Get recent files from BAL project
				if (!string.IsNullOrEmpty(project.BalProjectPath) && Directory.Exists(project.BalProjectPath))
				{
					var balFiles = Directory.GetFiles(project.BalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddDays(-7)) // Files from last week
						.Select(f => (f, "BAL", File.GetLastWriteTime(f)))
						.OrderByDescending(f => f.Item3);

					recentFiles.AddRange(balFiles);
				}

				// Take the most recent files as examples
				var selectedFiles = recentFiles
					.OrderByDescending(f => f.modified)
					.Take(maxAdditional)
					.ToList();

				foreach (var (filePath, fileType, modified) in selectedFiles)
				{
					try
					{
						var content = await File.ReadAllTextAsync(filePath);
						var relativePath = fileType == "DAL"
							? Path.GetRelativePath(project.DalProjectPath, filePath)
							: Path.GetRelativePath(project.BalProjectPath, filePath);

						examples.Add($"=== EXAMPLE {fileType} FILE: {relativePath} ===\n{content}");
						_logger.LogInformation($"Added additional example: {relativePath}");
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, $"Could not read example file: {filePath}");
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error getting additional project examples");
			}

			return examples;
		}

		private FileDependencies AnalyzeDependencies(ProjectFile file)
		{
			var dependencies = new FileDependencies
			{
				Classes = ExtractClassNames(file.SourceCode),
				Namespaces = ExtractNamespaces(file.SourceCode),
				Methods = ExtractMethodNames(file.SourceCode),
				Properties = ExtractPropertyNames(file.SourceCode),
				DatabaseTables = ExtractDatabaseTableReferences(file.SourceCode),
				BusinessEntities = ExtractBusinessEntityReferences(file.SourceCode),
				CustomTypes = ExtractCustomTypeReferences(file.SourceCode),
				Interfaces = ExtractInterfaceReferences(file.SourceCode),
				ProjectReferences = ExtractProjectReferences(file.SourceCode)
			};

			return dependencies;
		}

		private bool IsRelatedFile(ProjectFile currentFile, ProjectFile potentialRelated, FileDependencies currentDependencies)
		{
			// Check various relationship types

			// 1. Same entity/domain (e.g., User, Product, Order)
			var currentEntity = ExtractEntityName(currentFile.FileName);
			var relatedEntity = ExtractEntityName(potentialRelated.FileName);
			if (!string.IsNullOrEmpty(currentEntity) && !string.IsNullOrEmpty(relatedEntity) &&
				currentEntity.Equals(relatedEntity, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// 2. Class name references
			foreach (var className in currentDependencies.Classes)
			{
				if (potentialRelated.Classes.Contains(className, StringComparer.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// 3. Database table references
			foreach (var table in currentDependencies.DatabaseTables)
			{
				if (potentialRelated.SourceCode.Contains(table, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// 4. Business entity references
			foreach (var entity in currentDependencies.BusinessEntities)
			{
				if (potentialRelated.FileName.Contains(entity, StringComparison.OrdinalIgnoreCase) ||
					potentialRelated.SourceCode.Contains(entity, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// 5. File path similarity (same folder or related folders)
			if (ArePathsRelated(currentFile.FilePath, potentialRelated.FilePath))
			{
				return true;
			}

			// 6. Interface implementations (IUserService -> UserService)
			var currentEntity = ExtractEntityName(currentFile.FileName);
			var relatedEntity = ExtractEntityName(potentialRelated.FileName);
			if (!string.IsNullOrEmpty(currentEntity) && !string.IsNullOrEmpty(relatedEntity))
			{
				if (currentFile.SourceCode.Contains($"I{relatedEntity}", StringComparison.OrdinalIgnoreCase) || 
					potentialRelated.SourceCode.Contains($"I{currentEntity}", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// 7. Interface references
			foreach (var interfaceRef in currentDependencies.Interfaces)
			{
				if (potentialRelated.SourceCode.Contains(interfaceRef, StringComparison.OrdinalIgnoreCase) ||
					potentialRelated.FileName.Contains(interfaceRef.Replace("I", ""), StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// 8. Project namespace references
			var relatedDependencies = AnalyzeDependencies(potentialRelated);
			foreach (var projectRef in currentDependencies.ProjectReferences)
			{
				if (relatedDependencies.ProjectReferences.Any(r => r.Contains(projectRef, StringComparison.OrdinalIgnoreCase)))
				{
					return true;
				}
			}

			// 9. Namespace overlap
			var currentNamespaces = currentDependencies.Namespaces;
			if (currentNamespaces.Intersect(relatedDependencies.Namespaces, StringComparer.OrdinalIgnoreCase).Any())
			{
				return true;
			}

			// 10. DAL/BAL layer relationship (UserDAL <-> UserBAL)
			if (!string.IsNullOrEmpty(currentEntity) && !string.IsNullOrEmpty(relatedEntity) &&
				currentEntity.Equals(relatedEntity, StringComparison.OrdinalIgnoreCase))
			{
				var currentIsDAL = currentFile.FileName.Contains("DAL", StringComparison.OrdinalIgnoreCase) ||
								  currentFile.FilePath.Contains("DataAccess", StringComparison.OrdinalIgnoreCase);
				var relatedIsBAL = potentialRelated.FileName.Contains("BAL", StringComparison.OrdinalIgnoreCase) ||
								  potentialRelated.FilePath.Contains("BusinessLogics", StringComparison.OrdinalIgnoreCase);
				var currentIsBAL = currentFile.FileName.Contains("BAL", StringComparison.OrdinalIgnoreCase) ||
								  currentFile.FilePath.Contains("BusinessLogics", StringComparison.OrdinalIgnoreCase);
				var relatedIsDAL = potentialRelated.FileName.Contains("DAL", StringComparison.OrdinalIgnoreCase) ||
								  potentialRelated.FilePath.Contains("DataAccess", StringComparison.OrdinalIgnoreCase);

				if ((currentIsDAL && relatedIsBAL) || (currentIsBAL && relatedIsDAL))
				{
					return true;
				}
			}

			return false;
		}

		private List<ProjectFile> SortByRelevance(ProjectFile currentFile, List<ProjectFile> relatedFiles)
		{
			return relatedFiles.OrderByDescending(f => CalculateRelevanceScore(currentFile, f)).ToList();
		}

		private int CalculateRelevanceScore(ProjectFile currentFile, ProjectFile relatedFile)
		{
			int score = 0;

			// Same entity name gets highest score
			var currentEntity = ExtractEntityName(currentFile.FileName);
			var relatedEntity = ExtractEntityName(relatedFile.FileName);
			if (!string.IsNullOrEmpty(currentEntity) && !string.IsNullOrEmpty(relatedEntity) &&
				currentEntity.Equals(relatedEntity, StringComparison.OrdinalIgnoreCase))
			{
				score += 100;
			}

			// Same file type but different layer (e.g., UserRepository and UserService)
			if (currentFile.FileType != relatedFile.FileType &&
				ExtractEntityName(currentFile.FileName) == ExtractEntityName(relatedFile.FileName))
			{
				score += 80;
			}

			// Similar paths
			if (ArePathsRelated(currentFile.FilePath, relatedFile.FilePath))
			{
				score += 50;
			}

			// Recently migrated files are more relevant
			if (relatedFile.MigratedAt.HasValue)
			{
				var daysSinceMigration = (DateTime.UtcNow - relatedFile.MigratedAt.Value).TotalDays;
				score += Math.Max(0, 30 - (int)daysSinceMigration);
			}

			// Higher complexity files that were successfully migrated are good examples
			score += relatedFile.Complexity * 5;

			return score;
		}

		private async Task<string> GetMigratedFileContentAsync(ProjectFile file, MigrationProject project)
		{
			var content = new List<string>();

			try
			{
				_logger.LogInformation($"Getting migrated content for file: {file.FileName}");

				// Get DAL files from the actual migrated DAL project
				if (!string.IsNullOrEmpty(file.DalOutputPath) && !string.IsNullOrEmpty(project.DalProjectPath))
				{
					var dalFiles = file.DalOutputPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
					foreach (var dalFile in dalFiles)
					{
						var fullPath = Path.Combine(project.DalProjectPath, dalFile.Trim());
						if (File.Exists(fullPath))
						{
							var fileContent = await File.ReadAllTextAsync(fullPath);
							content.Add($"=== MIGRATED DAL FILE: {dalFile} ===\n{fileContent}");
							_logger.LogInformation($"Included DAL file: {dalFile}");
						}
						else
						{
							_logger.LogWarning($"DAL file not found: {fullPath}");
						}
					}
				}

				// Get BAL files from the actual migrated BAL project
				if (!string.IsNullOrEmpty(file.BalOutputPath) && !string.IsNullOrEmpty(project.BalProjectPath))
				{
					var balFiles = file.BalOutputPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
					foreach (var balFile in balFiles)
					{
						var fullPath = Path.Combine(project.BalProjectPath, balFile.Trim());
						if (File.Exists(fullPath))
						{
							var fileContent = await File.ReadAllTextAsync(fullPath);
							content.Add($"=== MIGRATED BAL FILE: {balFile} ===\n{fileContent}");
							_logger.LogInformation($"Included BAL file: {balFile}");
						}
						else
						{
							_logger.LogWarning($"BAL file not found: {fullPath}");
						}
					}
				}

				// If no migrated files found, fall back to checking recent files in the project
				if (!content.Any())
				{
					_logger.LogInformation($"No tracked migrated files found for {file.FileName}, checking for recent files by entity name");
					content.AddRange(await GetRecentMigratedFilesByEntityAsync(file, project));
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting migrated file content for {file.FileName}");
			}

			return string.Join("\n\n", content);
		}

		private async Task<List<string>> GetRecentMigratedFilesByEntityAsync(ProjectFile file, MigrationProject project)
		{
			var content = new List<string>();
			var entityName = ExtractEntityName(file.FileName);

			if (string.IsNullOrEmpty(entityName))
				return content;

			try
			{
				// Check DAL project for entity-related files
				if (!string.IsNullOrEmpty(project.DalProjectPath) && Directory.Exists(project.DalProjectPath))
				{
					var dalFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => Path.GetFileNameWithoutExtension(f).Contains(entityName, StringComparison.OrdinalIgnoreCase))
						.OrderByDescending(f => File.GetLastWriteTime(f))
						.Take(3); // Get up to 3 most recent entity-related files

					foreach (var dalFile in dalFiles)
					{
						var fileContent = await File.ReadAllTextAsync(dalFile);
						var relativePath = Path.GetRelativePath(project.DalProjectPath, dalFile);
						content.Add($"=== RECENT DAL FILE: {relativePath} ===\n{fileContent}");
					}
				}

				// Check BAL project for entity-related files
				if (!string.IsNullOrEmpty(project.BalProjectPath) && Directory.Exists(project.BalProjectPath))
				{
					var balFiles = Directory.GetFiles(project.BalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => Path.GetFileNameWithoutExtension(f).Contains(entityName, StringComparison.OrdinalIgnoreCase))
						.OrderByDescending(f => File.GetLastWriteTime(f))
						.Take(3); // Get up to 3 most recent entity-related files

					foreach (var balFile in balFiles)
					{
						var fileContent = await File.ReadAllTextAsync(balFile);
						var relativePath = Path.GetRelativePath(project.BalProjectPath, balFile);
						content.Add($"=== RECENT BAL FILE: {relativePath} ===\n{fileContent}");
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, $"Error getting recent migrated files by entity for {entityName}");
			}

			return content;
		}

		public async Task<List<ProjectFile>> GetAllRelatedFilesAsync(ProjectFile currentFile, string projectId)
		{
			try
			{
				_logger.LogInformation($"Analyzing all dependencies (migrated + unmigrated) for file: {currentFile.FileName}");

				// Get both migrated and unmigrated files
				var migratedFiles = await _repository.GetProjectFilesAsync(projectId, "migrated");
				var unmigratedFiles = await _repository.GetProjectFilesAsync(projectId, "pending");

				var dependencies = AnalyzeDependencies(currentFile);
				var relatedFiles = new List<ProjectFile>();

				// Combine all files and prioritize migrated versions
				var allFiles = migratedFiles.Concat(unmigratedFiles).ToList();

				// Group by entity name and prioritize migrated files
				var filesByEntity = allFiles
					.GroupBy(f => ExtractEntityName(f.FileName))
					.ToDictionary(g => g.Key, g => g.OrderBy(f => f.Status == "migrated" ? 0 : 1).ToList());

				foreach (var fileGroup in filesByEntity.Values)
				{
					var primaryFile = fileGroup.First(); // Migrated version if available, otherwise unmigrated
					
					if (IsRelatedFile(currentFile, primaryFile, dependencies))
					{
						relatedFiles.Add(primaryFile);
						_logger.LogInformation($"Found related file: {primaryFile.FileName} (Status: {primaryFile.Status})");
					}
				}

				return SortByRelevance(currentFile, relatedFiles);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing all dependencies for {currentFile.FileName}");
				return new List<ProjectFile>();
			}
		}

		public async Task<DependencyStatus> GetDependencyStatusAsync(ProjectFile currentFile, string projectId)
		{
			try
			{
				var allRelated = await GetAllRelatedFilesAsync(currentFile, projectId);
				var dependencies = AnalyzeDependencies(currentFile);

				var status = new DependencyStatus
				{
					MigratedDependencies = allRelated.Where(f => f.Status == "migrated").ToList(),
					UnmigratedDependencies = allRelated.Where(f => f.Status != "migrated").ToList()
				};

				// Check for missing dependencies (referenced but not found)
				foreach (var interfaceRef in dependencies.Interfaces)
				{
					bool found = allRelated.Any(f => 
						f.SourceCode.Contains(interfaceRef, StringComparison.OrdinalIgnoreCase) ||
						f.FileName.Contains(interfaceRef.Replace("I", ""), StringComparison.OrdinalIgnoreCase));
					
					if (!found)
					{
						status.MissingDependencies.Add(interfaceRef);
					}
				}

				_logger.LogInformation($"Dependency status for {currentFile.FileName}: {status.MigratedDependencies.Count} migrated, {status.UnmigratedDependencies.Count} unmigrated, {status.MissingDependencies.Count} missing");

				return status;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting dependency status for {currentFile.FileName}");
				return new DependencyStatus();
			}
		}

		private List<string> ExtractClassNames(string sourceCode)
		{
			var classes = new List<string>();
			var matches = Regex.Matches(sourceCode, @"(?:public|internal|private)\s+(?:partial\s+)?class\s+(\w+)", RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					classes.Add(match.Groups[1].Value);
				}
			}

			return classes.Distinct().ToList();
		}

		private List<string> ExtractNamespaces(string sourceCode)
		{
			var namespaces = new List<string>();
			var matches = Regex.Matches(sourceCode, @"using\s+([\w\.]+);", RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					namespaces.Add(match.Groups[1].Value);
				}
			}

			return namespaces.Distinct().ToList();
		}

		private List<string> ExtractMethodNames(string sourceCode)
		{
			var methods = new List<string>();
			var matches = Regex.Matches(sourceCode, @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:\w+\s+)?(\w+)\s*\([^)]*\)", RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1 && !IsPropertyAccessor(match.Groups[1].Value))
				{
					methods.Add(match.Groups[1].Value);
				}
			}

			return methods.Distinct().ToList();
		}

		private List<string> ExtractPropertyNames(string sourceCode)
		{
			var properties = new List<string>();
			var matches = Regex.Matches(sourceCode, @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:\w+\s+)?(\w+)\s*{\s*(?:get|set)", RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					properties.Add(match.Groups[1].Value);
				}
			}

			return properties.Distinct().ToList();
		}

		private List<string> ExtractDatabaseTableReferences(string sourceCode)
		{
			var tables = new List<string>();

			// Look for SQL table references
			var sqlMatches = Regex.Matches(sourceCode, @"FROM\s+(\w+)|INSERT\s+INTO\s+(\w+)|UPDATE\s+(\w+)|DELETE\s+FROM\s+(\w+)", RegexOptions.IgnoreCase);

			foreach (Match match in sqlMatches)
			{
				for (int i = 1; i < match.Groups.Count; i++)
				{
					if (match.Groups[i].Success && !string.IsNullOrEmpty(match.Groups[i].Value))
					{
						tables.Add(match.Groups[i].Value);
					}
				}
			}

			return tables.Distinct().ToList();
		}

		private List<string> ExtractBusinessEntityReferences(string sourceCode)
		{
			var entities = new List<string>();

			// Common business entity patterns
			var patterns = new[]
			{
				@"\b(User|Customer|Product|Order|Invoice|Payment|Account|Employee|Department)\b",
				@"\b(\w+)Entity\b",
				@"\b(\w+)Model\b",
				@"\b(\w+)Dto\b"
			};

			foreach (var pattern in patterns)
			{
				var matches = Regex.Matches(sourceCode, pattern, RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					if (match.Groups.Count > 1)
					{
						entities.Add(match.Groups[1].Value);
					}
				}
			}

			return entities.Distinct().ToList();
		}

		private List<string> ExtractCustomTypeReferences(string sourceCode)
		{
			var types = new List<string>();

			// Look for custom type instantiations
			var matches = Regex.Matches(sourceCode, @"new\s+(\w+)\s*\(", RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					var typeName = match.Groups[1].Value;
					if (!IsBuiltInType(typeName))
					{
						types.Add(typeName);
					}
				}
			}

			return types.Distinct().ToList();
		}

		private string ExtractEntityName(string fileName)
		{
			// Remove common prefixes/suffixes to get entity name
			var name = Path.GetFileNameWithoutExtension(fileName);

			// Remove common patterns
			var patterns = new[]
			{
				@"(.+)Repository$",
				@"(.+)Service$",
				@"(.+)Manager$",
				@"(.+)Controller$",
				@"(.+)Dal$",
				@"(.+)Bal$",
				@"(.+)Business$",
				@"(.+)Data$",
				@"(.+)Logic$"
			};

			foreach (var pattern in patterns)
			{
				var match = Regex.Match(name, pattern, RegexOptions.IgnoreCase);
				if (match.Success && match.Groups.Count > 1)
				{
					return match.Groups[1].Value;
				}
			}

			return name;
		}

		private bool ArePathsRelated(string path1, string path2)
		{
			var dir1 = Path.GetDirectoryName(path1);
			var dir2 = Path.GetDirectoryName(path2);

			// Same directory
			if (string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// Parent/child relationship
			if (!string.IsNullOrEmpty(dir1) && !string.IsNullOrEmpty(dir2))
			{
				return dir1.Contains(dir2, StringComparison.OrdinalIgnoreCase) ||
					   dir2.Contains(dir1, StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}

		private bool IsPropertyAccessor(string methodName)
		{
			return methodName.Equals("get", StringComparison.OrdinalIgnoreCase) ||
				   methodName.Equals("set", StringComparison.OrdinalIgnoreCase);
		}

		private bool IsBuiltInType(string typeName)
		{
			var builtInTypes = new[]
			{
				"string", "int", "bool", "double", "float", "decimal", "long", "short", "byte",
				"char", "object", "DateTime", "TimeSpan", "Guid", "List", "Dictionary", "Array"
			};

			return builtInTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase);
		}

		private List<string> ExtractInterfaceReferences(string sourceCode)
		{
			var interfaces = new List<string>();

			// Interface implementations (class X : IY)
			var implementsMatches = Regex.Matches(sourceCode, @"class\s+\w+\s*:\s*(I\w+)", RegexOptions.IgnoreCase);
			foreach (Match match in implementsMatches)
			{
				if (match.Groups.Count > 1)
				{
					interfaces.Add(match.Groups[1].Value);
				}
			}

			// Interface declarations
			var declarationMatches = Regex.Matches(sourceCode, @"(?:public|internal|private)\s+interface\s+(I\w+)", RegexOptions.IgnoreCase);
			foreach (Match match in declarationMatches)
			{
				if (match.Groups.Count > 1)
				{
					interfaces.Add(match.Groups[1].Value);
				}
			}

			// Interface usage in method parameters and variables
			var usageMatches = Regex.Matches(sourceCode, @"\b(I[A-Z]\w+)\b", RegexOptions.IgnoreCase);
			foreach (Match match in usageMatches)
			{
				interfaces.Add(match.Groups[1].Value);
			}

			return interfaces.Distinct().ToList();
		}

		private List<string> ExtractProjectReferences(string sourceCode)
		{
			var references = new List<string>();

			// Look for namespace references that might indicate cross-project dependencies
			var namespacePatterns = new[]
			{
				@"using\s+([\w\.]+\.DAL[\w\.]*);",
				@"using\s+([\w\.]+\.BAL[\w\.]*);",
				@"using\s+([\w\.]+\.DataAccess[\w\.]*);",
				@"using\s+([\w\.]+\.BusinessLogics[\w\.]*);",
				@"using\s+([\w\.]+\.Services[\w\.]*);",
				@"using\s+([\w\.]+\.Models[\w\.]*);",
				@"using\s+([\w\.]+\.Utils[\w\.]*);",
			};

			foreach (var pattern in namespacePatterns)
			{
				var matches = Regex.Matches(sourceCode, pattern, RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					if (match.Groups.Count > 1)
					{
						references.Add(match.Groups[1].Value);
					}
				}
			}

			return references.Distinct().ToList();
		}
	}

	// Supporting classes
	public class FileDependencies
	{
		public List<string> Classes { get; set; } = new List<string>();
		public List<string> Namespaces { get; set; } = new List<string>();
		public List<string> Methods { get; set; } = new List<string>();
		public List<string> Properties { get; set; } = new List<string>();
		public List<string> DatabaseTables { get; set; } = new List<string>();
		public List<string> BusinessEntities { get; set; } = new List<string>();
		public List<string> CustomTypes { get; set; } = new List<string>();
		public List<string> Interfaces { get; set; } = new List<string>();
		public List<string> ProjectReferences { get; set; } = new List<string>();
	}

	public class DependencyStatus
	{
		public List<ProjectFile> MigratedDependencies { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> UnmigratedDependencies { get; set; } = new List<ProjectFile>();
		public List<string> MissingDependencies { get; set; } = new List<string>();
	}

	public interface IDependencyAnalyzer
	{
		Task<List<ProjectFile>> GetRelatedMigratedFilesAsync(ProjectFile currentFile, string projectId);
		Task<List<string>> GetRelatedMigratedFileContentsAsync(ProjectFile currentFile, string projectId, MigrationProject project);
		Task<List<ProjectFile>> GetAllRelatedFilesAsync(ProjectFile currentFile, string projectId);
		Task<DependencyStatus> GetDependencyStatusAsync(ProjectFile currentFile, string projectId);
	}
}