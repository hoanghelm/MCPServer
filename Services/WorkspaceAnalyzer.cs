using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Services
{
	public class WorkspaceAnalyzer : IWorkspaceAnalyzer
	{
		private readonly ILogger<WorkspaceAnalyzer> _logger;
		private readonly IDataRepository _repository;
		private readonly IArchitectureAnalyzer _architectureAnalyzer;

		public WorkspaceAnalyzer(ILogger<WorkspaceAnalyzer> logger, IDataRepository repository, IArchitectureAnalyzer architectureAnalyzer)
		{
			_logger = logger;
			_repository = repository;
			_architectureAnalyzer = architectureAnalyzer;
		}

		public async Task<WorkspaceAnalysis> AnalyzeWorkspaceAsync(string workspacePath)
		{
			try
			{
				_logger.LogInformation($"Starting workspace analysis for: {workspacePath}");

				if (!Directory.Exists(workspacePath))
				{
					throw new DirectoryNotFoundException($"Workspace not found: {workspacePath}");
				}

				var analysis = new WorkspaceAnalysis
				{
					WorkspacePath = workspacePath,
					ProjectName = GetProjectName(workspacePath)
				};

				// Find and analyze all relevant files (C# and VB.NET)
				await FindAndAnalyzeFilesAsync(workspacePath, analysis);

				// Analyze existing project architecture
				var architecture = await _architectureAnalyzer.AnalyzeProjectArchitectureAsync(workspacePath);
				analysis.ProjectArchitecture = architecture;

				// Calculate statistics
				CalculateStatistics(analysis);

				// Save analysis to database
				await _repository.SaveWorkspaceAnalysisAsync(analysis);

				_logger.LogInformation($"Workspace analysis completed. Found {analysis.TotalFiles} files to analyze.");

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing workspace: {workspacePath}");
				throw new MigrationException($"Failed to analyze workspace: {ex.Message}", ex);
			}
		}

		private async Task FindAndAnalyzeFilesAsync(string workspacePath, WorkspaceAnalysis analysis)
		{
			// Find C# files (exclude designer files)
			var csFiles = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories)
				.Where(f => !IsExcludedPath(f) && !IsDesignerFile(f))
				.ToList();

			// Find VB.NET files (exclude designer files)
			var vbFiles = Directory.GetFiles(workspacePath, "*.vb", SearchOption.AllDirectories)
				.Where(f => !IsExcludedPath(f) && !IsDesignerFile(f))
				.ToList();

			// Find ASPX files
			var aspxFiles = Directory.GetFiles(workspacePath, "*.aspx", SearchOption.AllDirectories)
				.Where(f => !IsExcludedPath(f))
				.ToList();

			// Find ASCX files (User Controls)
			var ascxFiles = Directory.GetFiles(workspacePath, "*.ascx", SearchOption.AllDirectories)
				.Where(f => !IsExcludedPath(f))
				.ToList();

			analysis.TotalFiles = csFiles.Count + vbFiles.Count + aspxFiles.Count + ascxFiles.Count;

			_logger.LogInformation($"Found files - C#: {csFiles.Count}, VB.NET: {vbFiles.Count}, ASPX: {aspxFiles.Count}, ASCX: {ascxFiles.Count}");

			// Analyze C# files
			foreach (var filePath in csFiles)
			{
				var file = await AnalyzeCsFileAsync(filePath, analysis.ProjectId);
				analysis.CsFiles.Add(file);
				CategorizeFile(file, analysis);
			}

			// Analyze VB.NET files
			foreach (var filePath in vbFiles)
			{
				var file = await AnalyzeVbFileAsync(filePath, analysis.ProjectId);
				analysis.CsFiles.Add(file); // Add to same collection for unified processing
				CategorizeFile(file, analysis);
			}

			// Analyze ASPX files
			foreach (var filePath in aspxFiles)
			{
				var file = new ProjectFile
				{
					ProjectId = analysis.ProjectId,
					FilePath = filePath,
					FileName = Path.GetFileName(filePath),
					FileType = FileType.WebForm.ToString()
				};

				analysis.AspxFiles.Add(file);
				analysis.WebForms.Add(file);

				// Check for code-behind files (both C# and VB.NET)
				await CheckForCodeBehind(filePath, analysis);
			}

			// Analyze ASCX files
			foreach (var filePath in ascxFiles)
			{
				var file = new ProjectFile
				{
					ProjectId = analysis.ProjectId,
					FilePath = filePath,
					FileName = Path.GetFileName(filePath),
					FileType = FileType.UserControl.ToString()
				};

				analysis.AscxFiles.Add(file);

				// Check for code-behind files (both C# and VB.NET)
				await CheckForCodeBehind(filePath, analysis);
			}
		}

		private async Task<ProjectFile> AnalyzeCsFileAsync(string filePath, string projectId)
		{
			var file = new ProjectFile
			{
				ProjectId = projectId,
				FilePath = filePath,
				FileName = Path.GetFileName(filePath),
				FileType = FileType.Unknown.ToString()
			};

			try
			{
				file.SourceCode = await File.ReadAllTextAsync(filePath);

				// Analyze C# code
				file.Classes = ExtractCsClassNames(file.SourceCode);
				file.Dependencies = ExtractCsDependencies(file.SourceCode);
				file.Complexity = CalculateComplexity(file.SourceCode);
				file.FileType = DetermineCsFileType(file.SourceCode, filePath);

				_logger.LogDebug($"Analyzed C# file: {file.FileName} - Type: {file.FileType}, Complexity: {file.Complexity}");
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Failed to analyze C# file {filePath}: {ex.Message}");
			}

			return file;
		}

		private async Task<ProjectFile> AnalyzeVbFileAsync(string filePath, string projectId)
		{
			var file = new ProjectFile
			{
				ProjectId = projectId,
				FilePath = filePath,
				FileName = Path.GetFileName(filePath),
				FileType = FileType.Unknown.ToString()
			};

			try
			{
				file.SourceCode = await File.ReadAllTextAsync(filePath);

				// Analyze VB.NET code
				file.Classes = ExtractVbClassNames(file.SourceCode);
				file.Dependencies = ExtractVbDependencies(file.SourceCode);
				file.Complexity = CalculateComplexity(file.SourceCode);
				file.FileType = DetermineVbFileType(file.SourceCode, filePath);

				_logger.LogDebug($"Analyzed VB.NET file: {file.FileName} - Type: {file.FileType}, Complexity: {file.Complexity}");
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Failed to analyze VB.NET file {filePath}: {ex.Message}");
			}

			return file;
		}

		private async Task CheckForCodeBehind(string webFormPath, WorkspaceAnalysis analysis)
		{
			// Check for C# code-behind
			var csCodeBehindPath = webFormPath + ".cs";
			if (File.Exists(csCodeBehindPath))
			{
				var codeBehindFile = analysis.CsFiles.FirstOrDefault(f => f.FilePath == csCodeBehindPath);
				if (codeBehindFile != null)
				{
					codeBehindFile.FileType = FileType.CodeBehind.ToString();
					_logger.LogDebug($"Marked C# code-behind: {codeBehindFile.FileName}");
				}
			}

			// Check for VB.NET code-behind
			var vbCodeBehindPath = webFormPath + ".vb";
			if (File.Exists(vbCodeBehindPath))
			{
				var codeBehindFile = analysis.CsFiles.FirstOrDefault(f => f.FilePath == vbCodeBehindPath);
				if (codeBehindFile != null)
				{
					codeBehindFile.FileType = FileType.CodeBehind.ToString();
					_logger.LogDebug($"Marked VB.NET code-behind: {codeBehindFile.FileName}");
				}
			}
		}

		private void CategorizeFile(ProjectFile file, WorkspaceAnalysis analysis)
		{
			switch (file.FileType)
			{
				case "DataAccess":
					analysis.DataAccessFiles.Add(file);
					break;
				case "BusinessLogic":
					analysis.BusinessLogicFiles.Add(file);
					break;
				case "WebForm":
				case "CodeBehind":
					analysis.WebForms.Add(file);
					break;
			}
		}

		private bool IsExcludedPath(string path)
		{
			var excludedDirs = new[] { "bin", "obj", "packages", ".vs", ".git", "node_modules", "Properties", "App_Data" };
			var pathParts = path.Split(Path.DirectorySeparatorChar);

			return pathParts.Any(part => excludedDirs.Contains(part, StringComparer.OrdinalIgnoreCase));
		}

		private bool IsDesignerFile(string filePath)
		{
			var fileName = Path.GetFileName(filePath).ToLower();

			// Exclude designer files
			var designerPatterns = new[]
			{
				".designer.cs",
				".designer.vb",
				".aspx.designer.cs",
				".aspx.designer.vb",
				".ascx.designer.cs",
				".ascx.designer.vb",
				".master.designer.cs",
				".master.designer.vb",
				"assemblyinfo.cs",
				"assemblyinfo.vb",
				"globalasax.cs",
				"globalasax.vb",
				"global.asax.cs",
				"global.asax.vb"
			};

			return designerPatterns.Any(pattern => fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));
		}

		private string GetProjectName(string workspacePath)
		{
			// Try to find project files (C# or VB.NET)
			var projectFiles = Directory.GetFiles(workspacePath, "*.csproj", SearchOption.TopDirectoryOnly)
				.Concat(Directory.GetFiles(workspacePath, "*.vbproj", SearchOption.TopDirectoryOnly))
				.ToArray();

			if (projectFiles.Any())
			{
				return Path.GetFileNameWithoutExtension(projectFiles.First());
			}

			// Fallback to directory name
			return new DirectoryInfo(workspacePath).Name;
		}

		// C# specific analysis methods
		private List<string> ExtractCsClassNames(string sourceCode)
		{
			var classes = new List<string>();
			var lines = sourceCode.Split('\n');

			foreach (var line in lines)
			{
				var trimmed = line.Trim();
				if ((trimmed.Contains("class ") || trimmed.Contains("interface ")) && !trimmed.StartsWith("//"))
				{
					var classMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"(?:class|interface)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
					if (classMatch.Success)
					{
						classes.Add(classMatch.Groups[1].Value);
					}
				}
			}

			return classes;
		}

		private List<string> ExtractCsDependencies(string sourceCode)
		{
			var dependencies = new List<string>();
			var lines = sourceCode.Split('\n');

			foreach (var line in lines)
			{
				var trimmed = line.Trim();
				if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
				{
					var usingStatement = trimmed.Substring(6, trimmed.Length - 7).Trim();
					dependencies.Add(usingStatement);
				}
			}

			return dependencies.Distinct().ToList();
		}

		private string DetermineCsFileType(string sourceCode, string filePath)
		{
			var fileName = Path.GetFileName(filePath).ToLower();

			// Check by file name patterns
			if (fileName.EndsWith(".aspx.cs"))
				return FileType.CodeBehind.ToString();
			if (fileName.EndsWith(".ascx.cs"))
				return FileType.UserControl.ToString();

			// Check by content
			if (sourceCode.Contains("System.Web.UI.Page") || sourceCode.Contains(": Page"))
				return FileType.WebForm.ToString();
			if (sourceCode.Contains("SqlConnection") || sourceCode.Contains("SqlCommand") ||
				sourceCode.Contains("NpgsqlConnection") || sourceCode.Contains("NpgsqlCommand") ||
				fileName.Contains("dal") || fileName.Contains("dao") || fileName.Contains("repository"))
				return FileType.DataAccess.ToString();
			if (fileName.Contains("business") || fileName.Contains("service") || fileName.Contains("manager") ||
				fileName.Contains("bl") || fileName.Contains("logic"))
				return FileType.BusinessLogic.ToString();
			if (fileName.Contains("model") || fileName.Contains("entity") || fileName.Contains("dto"))
				return FileType.Model.ToString();
			if (fileName.Contains("util") || fileName.Contains("helper") || fileName.Contains("common"))
				return FileType.Utility.ToString();

			return FileType.Unknown.ToString();
		}

		// VB.NET specific analysis methods
		private List<string> ExtractVbClassNames(string sourceCode)
		{
			var classes = new List<string>();
			var lines = sourceCode.Split('\n');

			foreach (var line in lines)
			{
				var trimmed = line.Trim();
				if ((trimmed.StartsWith("Public Class", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.StartsWith("Private Class", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.StartsWith("Friend Class", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.StartsWith("Class ", StringComparison.OrdinalIgnoreCase)) &&
					!trimmed.StartsWith("'"))
				{
					var classMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"Class\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
					if (classMatch.Success)
					{
						classes.Add(classMatch.Groups[1].Value);
					}
				}
			}

			return classes;
		}

		private List<string> ExtractVbDependencies(string sourceCode)
		{
			var dependencies = new List<string>();
			var lines = sourceCode.Split('\n');

			foreach (var line in lines)
			{
				var trimmed = line.Trim();
				if (trimmed.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("'"))
				{
					var importStatement = trimmed.Substring(8).Trim();
					dependencies.Add(importStatement);
				}
			}

			return dependencies.Distinct().ToList();
		}

		private string DetermineVbFileType(string sourceCode, string filePath)
		{
			var fileName = Path.GetFileName(filePath).ToLower();

			// Check by file name patterns
			if (fileName.EndsWith(".aspx.vb"))
				return FileType.CodeBehind.ToString();
			if (fileName.EndsWith(".ascx.vb"))
				return FileType.UserControl.ToString();

			// Check by content
			if (sourceCode.Contains("System.Web.UI.Page") ||
				sourceCode.Contains("Inherits System.Web.UI.Page") ||
				sourceCode.Contains("Inherits Page"))
				return FileType.WebForm.ToString();
			if (sourceCode.Contains("SqlConnection") || sourceCode.Contains("SqlCommand") ||
				sourceCode.Contains("NpgsqlConnection") || sourceCode.Contains("NpgsqlCommand") ||
				fileName.Contains("dal") || fileName.Contains("dao") || fileName.Contains("repository"))
				return FileType.DataAccess.ToString();
			if (fileName.Contains("business") || fileName.Contains("service") || fileName.Contains("manager") ||
				fileName.Contains("bl") || fileName.Contains("logic"))
				return FileType.BusinessLogic.ToString();
			if (fileName.Contains("model") || fileName.Contains("entity") || fileName.Contains("dto"))
				return FileType.Model.ToString();
			if (fileName.Contains("util") || fileName.Contains("helper") || fileName.Contains("common"))
				return FileType.Utility.ToString();

			return FileType.Unknown.ToString();
		}

		private int CalculateComplexity(string sourceCode)
		{
			// Simple complexity calculation based on various factors
			var complexity = 1;

			// Count control structures (works for both C# and VB.NET)
			complexity += CountOccurrences(sourceCode, new[] {
				"if (", "if(", "If ", // C# and VB.NET if statements
                "while (", "while(", "While ", // C# and VB.NET while loops
                "for (", "for(", "For ", // C# and VB.NET for loops
                "foreach (", "foreach(", "For Each " // C# and VB.NET foreach
            });

			// Count methods (both C# and VB.NET)
			complexity += CountOccurrences(sourceCode, new[] {
				"public ", "private ", "protected ", "internal ", // C#
                "Public ", "Private ", "Protected ", "Friend " // VB.NET
            }) / 2;

			// Count database operations (both SQL Server and PostgreSQL)
			complexity += CountOccurrences(sourceCode, new[] {
				"SqlConnection", "SqlCommand", "DataSet", "DataTable",
				"NpgsqlConnection", "NpgsqlCommand", "NpgsqlDataAdapter", "NpgsqlDataReader"
			}) * 2;

			// Normalize to 1-5 scale
			return Math.Min(5, Math.Max(1, complexity / 5));
		}

		private int CountOccurrences(string text, string[] patterns)
		{
			return patterns.Sum(pattern =>
				text.Split(new[] { pattern }, StringSplitOptions.None).Length - 1);
		}

		private void CalculateStatistics(WorkspaceAnalysis analysis)
		{
			// Files by type
			analysis.FilesByType = new Dictionary<string, int>
			{
				["WebForms"] = analysis.WebForms.Count,
				["DataAccess"] = analysis.DataAccessFiles.Count,
				["BusinessLogic"] = analysis.BusinessLogicFiles.Count,
				["Models"] = analysis.CsFiles.Count(f => f.FileType == FileType.Model.ToString()),
				["Utilities"] = analysis.CsFiles.Count(f => f.FileType == FileType.Utility.ToString()),
				["Unknown"] = analysis.CsFiles.Count(f => f.FileType == FileType.Unknown.ToString())
			};

			// Files by complexity
			analysis.FilesByComplexity = new Dictionary<string, int>
			{
				["Low (1-2)"] = analysis.CsFiles.Count(f => f.Complexity <= 2),
				["Medium (3)"] = analysis.CsFiles.Count(f => f.Complexity == 3),
				["High (4-5)"] = analysis.CsFiles.Count(f => f.Complexity >= 4)
			};

			// Add language breakdown
			var csFileCount = analysis.CsFiles.Count(f => f.FilePath.EndsWith(".cs"));
			var vbFileCount = analysis.CsFiles.Count(f => f.FilePath.EndsWith(".vb"));

			analysis.FilesByType["C# Files"] = csFileCount;
			analysis.FilesByType["VB.NET Files"] = vbFileCount;

			_logger.LogInformation($"Analysis complete - C#: {csFileCount}, VB.NET: {vbFileCount}, Total: {analysis.TotalFiles}");
		}
	}
}