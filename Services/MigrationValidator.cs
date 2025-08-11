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
	public class MigrationValidator : IMigrationValidator
	{
		private readonly ILogger<MigrationValidator> _logger;
		private readonly IDataRepository _repository;
		private readonly IArchitectureAnalyzer _architectureAnalyzer;

		public MigrationValidator(ILogger<MigrationValidator> logger, IDataRepository repository, IArchitectureAnalyzer architectureAnalyzer)
		{
			_logger = logger;
			_repository = repository;
			_architectureAnalyzer = architectureAnalyzer;
		}

		public async Task<MigrationValidationResult> ValidateBeforeMigrationAsync(string projectId)
		{
			var result = new MigrationValidationResult
			{
				IsValid = true,
				ProjectId = projectId
			};

			try
			{
				_logger.LogInformation($"Starting pre-migration validation for project: {projectId}");

				var project = await _repository.GetMigrationProjectAsync(projectId);
				if (project == null)
				{
					result.IsValid = false;
					result.Issues.Add("Project not found in database");
					return result;
				}

				// 1. Validate project structure integrity
				await ValidateProjectStructure(project, result);

				// 2. Check original project state
				await ValidateOriginalProjectState(project, result);

				// 3. Validate migrated projects consistency
				await ValidateMigratedProjectsConsistency(project, result);

				// 4. Check file dependencies and references
				await ValidateFileDependencies(projectId, result);

				// 5. Validate database connectivity and schema
				await ValidateDatabaseConnectivity(project, result);

				// 6. Check for orphaned or missing files
				await ValidateFileIntegrity(project, result);

				// 7. Architecture consistency check
				await ValidateArchitectureConsistency(project, result);

				if (result.Issues.Any())
				{
					result.IsValid = false;
					_logger.LogWarning($"Validation found {result.Issues.Count} issues for project: {projectId}");
				}
				else
				{
					_logger.LogInformation($"Pre-migration validation passed for project: {projectId}");
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error during pre-migration validation for project: {projectId}");
				result.IsValid = false;
				result.Issues.Add($"Validation error: {ex.Message}");
				return result;
			}
		}

		public async Task<MigrationValidationResult> ValidateAfterMigrationAsync(ProjectFile migratedFile, MigrationProject project)
		{
			var result = new MigrationValidationResult
			{
				IsValid = true,
				ProjectId = project.ProjectId
			};

			try
			{
				_logger.LogInformation($"Starting post-migration validation for file: {migratedFile.FileName}");

				// 1. Validate created files exist and are valid
				await ValidateCreatedFiles(migratedFile, project, result);

				// 2. Check that migrated code compiles
				await ValidateCodeCompilation(migratedFile, project, result);

				// 3. Validate namespace consistency
				await ValidateNamespaceConsistency(migratedFile, project, result);

				// 4. Check interface implementations
				await ValidateInterfaceImplementations(migratedFile, project, result);

				// 5. Validate database operations
				await ValidateDatabaseOperations(migratedFile, project, result);

				// 6. Check dependency injection setup
				await ValidateDependencyInjection(migratedFile, project, result);

				if (result.Issues.Any())
				{
					result.IsValid = false;
					_logger.LogWarning($"Post-migration validation found {result.Issues.Count} issues for file: {migratedFile.FileName}");
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error during post-migration validation for file: {migratedFile.FileName}");
				result.IsValid = false;
				result.Issues.Add($"Post-migration validation error: {ex.Message}");
				return result;
			}
		}

		private async Task ValidateProjectStructure(MigrationProject project, MigrationValidationResult result)
		{
			// Check original project exists
			if (!Directory.Exists(project.WorkspacePath))
			{
				result.Issues.Add($"Original workspace path not found: {project.WorkspacePath}");
				return;
			}

			// Check DAL project structure
			if (!string.IsNullOrEmpty(project.DalProjectPath))
			{
				if (!Directory.Exists(project.DalProjectPath))
				{
					result.Issues.Add($"DAL project path not found: {project.DalProjectPath}");
				}
				else
				{
					var dalCsproj = Path.Combine(project.DalProjectPath, $"{project.ProjectName}.DAL.csproj");
					if (!File.Exists(dalCsproj))
					{
						result.Issues.Add($"DAL project file not found: {dalCsproj}");
					}
				}
			}

			// Check BAL project structure  
			if (!string.IsNullOrEmpty(project.BalProjectPath))
			{
				if (!Directory.Exists(project.BalProjectPath))
				{
					result.Issues.Add($"BAL project path not found: {project.BalProjectPath}");
				}
				else
				{
					var balCsproj = Path.Combine(project.BalProjectPath, $"{project.ProjectName}.BAL.csproj");
					if (!File.Exists(balCsproj))
					{
						result.Issues.Add($"BAL project file not found: {balCsproj}");
					}
				}
			}
		}

		private async Task ValidateOriginalProjectState(MigrationProject project, MigrationValidationResult result)
		{
			try
			{
				// Re-analyze architecture to detect changes
				var currentArchitecture = await _architectureAnalyzer.AnalyzeProjectArchitectureAsync(project.WorkspacePath);
				
				// Store current state for comparison
				result.OriginalProjectState = new OriginalProjectState
				{
					TotalCsFiles = Directory.GetFiles(project.WorkspacePath, "*.cs", SearchOption.AllDirectories).Length,
					TotalAspxFiles = Directory.GetFiles(project.WorkspacePath, "*.aspx", SearchOption.AllDirectories).Length,
					HasDataSets = currentArchitecture.UsesDataSets,
					HasExistingDAL = currentArchitecture.HasExistingDataLayer,
					HasExistingBAL = currentArchitecture.HasExistingBusinessLayer,
					Architecture = currentArchitecture
				};

				// Check for significant changes that might affect migration
				var allFiles = await _repository.GetProjectFilesAsync(project.ProjectId);
				var filesInDb = allFiles.Count;
				var filesOnDisk = result.OriginalProjectState.TotalCsFiles + result.OriginalProjectState.TotalAspxFiles;

				if (Math.Abs(filesInDb - filesOnDisk) > 5) // Allow small variance
				{
					result.Warnings.Add($"File count discrepancy: DB has {filesInDb} files, disk has {filesOnDisk} files");
				}
			}
			catch (Exception ex)
			{
				result.Issues.Add($"Error validating original project state: {ex.Message}");
			}
		}

		private async Task ValidateMigratedProjectsConsistency(MigrationProject project, MigrationValidationResult result)
		{
			try
			{
				var migratedFiles = await _repository.GetProjectFilesAsync(project.ProjectId, "migrated");
				
				result.MigratedProjectState = new MigratedProjectState
				{
					TotalMigratedFiles = migratedFiles.Count,
					DalFilesCount = 0,
					BalFilesCount = 0
				};

				foreach (var migratedFile in migratedFiles)
				{
					// Check DAL files exist
					if (!string.IsNullOrEmpty(migratedFile.DalOutputPath))
					{
						var dalFiles = migratedFile.DalOutputPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
						foreach (var dalFile in dalFiles)
						{
							var fullPath = Path.Combine(project.DalProjectPath, dalFile.Trim());
							if (!File.Exists(fullPath))
							{
								result.Issues.Add($"Missing DAL file: {fullPath} (referenced by {migratedFile.FileName})");
							}
							else
							{
								result.MigratedProjectState.DalFilesCount++;
							}
						}
					}

					// Check BAL files exist
					if (!string.IsNullOrEmpty(migratedFile.BalOutputPath))
					{
						var balFiles = migratedFile.BalOutputPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
						foreach (var balFile in balFiles)
						{
							var fullPath = Path.Combine(project.BalProjectPath, balFile.Trim());
							if (!File.Exists(fullPath))
							{
								result.Issues.Add($"Missing BAL file: {fullPath} (referenced by {migratedFile.FileName})");
							}
							else
							{
								result.MigratedProjectState.BalFilesCount++;
							}
						}
					}
				}

				// Check for orphaned files (files in DAL/BAL projects not tracked in database)
				if (Directory.Exists(project.DalProjectPath))
				{
					var dalCsFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
						.Select(f => Path.GetRelativePath(project.DalProjectPath, f))
						.ToList();

					var trackedDalFiles = migratedFiles
						.Where(mf => !string.IsNullOrEmpty(mf.DalOutputPath))
						.SelectMany(mf => mf.DalOutputPath.Split(';', StringSplitOptions.RemoveEmptyEntries))
						.Select(f => f.Trim())
						.ToHashSet();

					var orphanedDalFiles = dalCsFiles.Where(f => !trackedDalFiles.Contains(f)).ToList();
					if (orphanedDalFiles.Any())
					{
						result.Warnings.Add($"Orphaned DAL files found: {string.Join(", ", orphanedDalFiles.Take(5))}");
					}
				}
			}
			catch (Exception ex)
			{
				result.Issues.Add($"Error validating migrated projects consistency: {ex.Message}");
			}
		}

		private async Task ValidateFileDependencies(string projectId, MigrationValidationResult result)
		{
			try
			{
				var allFiles = await _repository.GetProjectFilesAsync(projectId);
				var migratedFiles = allFiles.Where(f => f.MigrationStatus == MigrationStatus.Completed).ToList();
				var pendingFiles = allFiles.Where(f => f.MigrationStatus == MigrationStatus.Pending).ToList();

				// Check for circular dependencies in pending files
				var dependencyMap = new Dictionary<string, List<string>>();
				
				foreach (var file in pendingFiles)
				{
					var dependencies = ExtractFileDependencies(file);
					dependencyMap[file.Id] = dependencies.Select(d => 
						allFiles.FirstOrDefault(f => f.FileName.Contains(d, StringComparison.OrdinalIgnoreCase))?.Id)
						.Where(id => id != null)
						.ToList();
				}

				// Detect circular dependencies
				var circularDeps = DetectCircularDependencies(dependencyMap);
				if (circularDeps.Any())
				{
					result.Warnings.Add($"Circular dependencies detected between files: {string.Join(", ", circularDeps)}");
				}

				// Check for broken references to migrated files
				foreach (var pendingFile in pendingFiles)
				{
					var dependencies = ExtractFileDependencies(pendingFile);
					foreach (var dep in dependencies)
					{
						var depFile = migratedFiles.FirstOrDefault(f => f.FileName.Contains(dep, StringComparison.OrdinalIgnoreCase));
						if (depFile != null)
						{
							// This pending file depends on a migrated file - good
							result.GoodDependencies.Add($"{pendingFile.FileName} -> {depFile.FileName} (migrated)");
						}
					}
				}
			}
			catch (Exception ex)
			{
				result.Issues.Add($"Error validating file dependencies: {ex.Message}");
			}
		}

		private async Task ValidateDatabaseConnectivity(MigrationProject project, MigrationValidationResult result)
		{
			// This would validate that PostgreSQL is accessible and has proper schema
			// For now, just add a placeholder
			result.DatabaseValidation = new DatabaseValidation
			{
				IsConnectable = true, // Would test actual connection
				HasRequiredSchema = true, // Would check schema exists
				Issues = new List<string>()
			};
		}

		private async Task ValidateFileIntegrity(MigrationProject project, MigrationValidationResult result)
		{
			try
			{
				var allFiles = await _repository.GetProjectFilesAsync(project.ProjectId);
				
				foreach (var file in allFiles)
				{
					// Check original file still exists
					if (!File.Exists(file.FilePath))
					{
						result.Issues.Add($"Original file missing: {file.FilePath}");
					}
					else
					{
						// Check if file has been modified since migration
						var lastModified = File.GetLastWriteTime(file.FilePath);
						if (file.MigratedAt.HasValue && lastModified > file.MigratedAt.Value)
						{
							result.Warnings.Add($"Original file modified after migration: {file.FileName} (consider re-migration)");
						}
					}
				}
			}
			catch (Exception ex)
			{
				result.Issues.Add($"Error validating file integrity: {ex.Message}");
			}
		}

		private async Task ValidateArchitectureConsistency(MigrationProject project, MigrationValidationResult result)
		{
			try
			{
				// Check that migrated files follow consistent architecture patterns
				if (!string.IsNullOrEmpty(project.DalProjectPath) && Directory.Exists(project.DalProjectPath))
				{
					var dalFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories);
					var namespaceIssues = new List<string>();

					foreach (var dalFile in dalFiles)
					{
						var content = await File.ReadAllTextAsync(dalFile);
						var expectedNamespace = $"{project.ProjectName}.DAL";
						
						if (!content.Contains($"namespace {expectedNamespace}"))
						{
							namespaceIssues.Add(Path.GetFileName(dalFile));
						}
					}

					if (namespaceIssues.Any())
					{
						result.Warnings.Add($"DAL files with inconsistent namespaces: {string.Join(", ", namespaceIssues.Take(5))}");
					}
				}
			}
			catch (Exception ex)
			{
				result.Issues.Add($"Error validating architecture consistency: {ex.Message}");
			}
		}

		private async Task ValidateCreatedFiles(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Validate that all created files are syntactically correct and follow patterns
		}

		private async Task ValidateCodeCompilation(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Would integrate with Roslyn to validate syntax
		}

		private async Task ValidateNamespaceConsistency(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Check namespace consistency across DAL/BAL
		}

		private async Task ValidateInterfaceImplementations(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Validate that interfaces and implementations match
		}

		private async Task ValidateDatabaseOperations(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Validate SQL syntax and PostgreSQL compatibility
		}

		private async Task ValidateDependencyInjection(ProjectFile migratedFile, MigrationProject project, MigrationValidationResult result)
		{
			// Check that DI is properly configured
		}

		private List<string> ExtractFileDependencies(ProjectFile file)
		{
			var dependencies = new List<string>();
			
			// Extract class names that might be dependencies
			var classMatches = Regex.Matches(file.SourceCode, @"\b([A-Z]\w+)(?:Service|Repository|Manager|Dal|Bal)\b");
			foreach (Match match in classMatches)
			{
				dependencies.Add(match.Groups[1].Value);
			}

			return dependencies.Distinct().ToList();
		}

		private List<string> DetectCircularDependencies(Dictionary<string, List<string>> dependencyMap)
		{
			// Simple circular dependency detection
			var circular = new List<string>();
			var visited = new HashSet<string>();
			var recursionStack = new HashSet<string>();

			foreach (var fileId in dependencyMap.Keys)
			{
				if (HasCircularDependency(fileId, dependencyMap, visited, recursionStack))
				{
					circular.Add(fileId);
				}
			}

			return circular;
		}

		private bool HasCircularDependency(string fileId, Dictionary<string, List<string>> dependencyMap, 
			HashSet<string> visited, HashSet<string> recursionStack)
		{
			if (recursionStack.Contains(fileId))
				return true;

			if (visited.Contains(fileId))
				return false;

			visited.Add(fileId);
			recursionStack.Add(fileId);

			if (dependencyMap.ContainsKey(fileId))
			{
				foreach (var dependency in dependencyMap[fileId])
				{
					if (HasCircularDependency(dependency, dependencyMap, visited, recursionStack))
						return true;
				}
			}

			recursionStack.Remove(fileId);
			return false;
		}
	}

	public interface IMigrationValidator
	{
		Task<MigrationValidationResult> ValidateBeforeMigrationAsync(string projectId);
		Task<MigrationValidationResult> ValidateAfterMigrationAsync(ProjectFile migratedFile, MigrationProject project);
	}
}