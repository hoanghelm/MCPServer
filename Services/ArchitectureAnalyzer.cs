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
	public class ArchitectureAnalyzer : IArchitectureAnalyzer
	{
		private readonly ILogger<ArchitectureAnalyzer> _logger;

		public ArchitectureAnalyzer(ILogger<ArchitectureAnalyzer> logger)
		{
			_logger = logger;
		}

		public async Task<ProjectArchitecture> AnalyzeProjectArchitectureAsync(string projectPath)
		{
			var architecture = new ProjectArchitecture
			{
				ProjectPath = projectPath,
				HasExistingDataLayer = false,
				HasExistingBusinessLayer = false,
				UsesDataSets = false,
				UsesTableAdapters = false
			};

			try
			{
				_logger.LogInformation($"Analyzing project architecture at: {projectPath}");

				var allFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
					.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
					.ToList();

				foreach (var file in allFiles)
				{
					var content = await File.ReadAllTextAsync(file);
					var relativePath = Path.GetRelativePath(projectPath, file);
					
					await AnalyzeFilePatterns(content, relativePath, architecture);
				}

				// Analyze folder structure
				AnalyzeFolderStructure(projectPath, architecture);

				// Determine extraction strategy
				architecture.ExtractionStrategy = DetermineExtractionStrategy(architecture);

				_logger.LogInformation($"Architecture analysis complete. Strategy: {architecture.ExtractionStrategy}");
				
				return architecture;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing project architecture: {projectPath}");
				return architecture;
			}
		}

		private async Task AnalyzeFilePatterns(string content, string relativePath, ProjectArchitecture architecture)
		{
			// 1. Detect DataSet/TableAdapter usage
			if (DetectDataSetUsage(content))
			{
				architecture.UsesDataSets = true;
				architecture.DataSetFiles.Add(new DataSetInfo 
				{ 
					FilePath = relativePath,
					TableAdapters = ExtractTableAdapters(content),
					DataTables = ExtractDataTables(content)
				});
			}

			// 2. Detect existing data access patterns
			if (DetectDataAccessPatterns(content))
			{
				architecture.HasExistingDataLayer = true;
				architecture.ExistingDataAccessFiles.Add(new DataAccessInfo
				{
					FilePath = relativePath,
					Pattern = DetectDataAccessPattern(content),
					DatabaseOperations = ExtractDatabaseOperations(content),
					ConnectionStrings = ExtractConnectionStrings(content)
				});
			}

			// 3. Detect existing business logic patterns
			if (DetectBusinessLogicPatterns(content))
			{
				architecture.HasExistingBusinessLayer = true;
				architecture.ExistingBusinessLogicFiles.Add(new BusinessLogicInfo
				{
					FilePath = relativePath,
					Pattern = DetectBusinessLogicPattern(content),
					BusinessMethods = ExtractBusinessMethods(content),
					ValidationMethods = ExtractValidationMethods(content)
				});
			}

			// 4. Detect code-behind with mixed logic
			if (relativePath.EndsWith(".aspx.cs") || relativePath.EndsWith(".ascx.cs"))
			{
				var webFormAnalysis = AnalyzeWebFormCodeBehind(content);
				architecture.WebFormFiles.Add(new WebFormInfo
				{
					FilePath = relativePath,
					HasDataAccess = webFormAnalysis.HasDataAccess,
					HasBusinessLogic = webFormAnalysis.HasBusinessLogic,
					ExtractableLogic = webFormAnalysis.ExtractableLogic,
					DatabaseOperations = webFormAnalysis.DatabaseOperations,
					BusinessOperations = webFormAnalysis.BusinessOperations
				});
			}
		}

		private bool DetectDataSetUsage(string content)
		{
			var dataSetPatterns = new[]
			{
				@"\bDataSet\b",
				@"\bTableAdapter\b", 
				@"\bTypedDataSet\b",
				@"\.Fill\s*\(",
				@"\.GetData\s*\(",
				@"\.Update\s*\(",
				@"\.Insert\s*\(",
				@"\.Delete\s*\(",
				@"DataSetName\w*TableAdapter",
				@"\.xsd\b"
			};

			return dataSetPatterns.Any(pattern => 
				Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
		}

		private bool DetectDataAccessPatterns(string content)
		{
			var dalPatterns = new[]
			{
				@"\bSqlConnection\b",
				@"\bSqlCommand\b",
				@"\bSqlDataReader\b",
				@"\bSqlDataAdapter\b",
				@"\bConnectionString\b",
				@"class\s+\w*(?:Dal|DAO|Repository|DataAccess)",
				@"namespace\s+\w*\.(?:Dal|DAO|DataAccess|Data)",
				@"\bEntityFramework\b",
				@"\bDbContext\b"
			};

			return dalPatterns.Any(pattern => 
				Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
		}

		private bool DetectBusinessLogicPatterns(string content)
		{
			var balPatterns = new[]
			{
				@"class\s+\w*(?:Bal|Business|Service|Logic|Manager)",
				@"namespace\s+\w*\.(?:Bal|Business|Logic|Services)",
				@"public\s+(?:async\s+)?(?:\w+\s+)?\w*(?:Validate|Process|Calculate|Execute)",
				@"(?:Validate|Business|Process).*\(",
				@"throw new (?:BusinessException|ValidationException)",
				@"business.*rules?",
				@"validation.*logic"
			};

			return balPatterns.Any(pattern => 
				Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
		}

		private void AnalyzeFolderStructure(string projectPath, ProjectArchitecture architecture)
		{
			var directories = Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories)
				.Select(d => Path.GetRelativePath(projectPath, d).ToLower())
				.ToList();

			// Check for existing layer folders
			var layerFolders = new[]
			{
				"dal", "data", "dataaccess", "repository", "repositories",
				"bal", "business", "businesslogic", "logic", "services",
				"models", "entities", "dto", "viewmodels"
			};

			architecture.ExistingLayerFolders.AddRange(
				directories.Where(dir => layerFolders.Any(layer => 
					dir.Contains(layer) || dir.Split('\\').Any(part => layer.Equals(part)))));
		}

		private ExtractionStrategy DetermineExtractionStrategy(ProjectArchitecture architecture)
		{
			// Strategy 1: Already has good separation
			if (architecture.HasExistingDataLayer && architecture.HasExistingBusinessLayer)
			{
				return ExtractionStrategy.ReuseAndEnhance;
			}

			// Strategy 2: Has DataSets - need modernization
			if (architecture.UsesDataSets)
			{
				return ExtractionStrategy.ModernizeDataSets;
			}

			// Strategy 3: Has some separation but incomplete
			if (architecture.HasExistingDataLayer || architecture.HasExistingBusinessLayer)
			{
				return ExtractionStrategy.ExtendExisting;
			}

			// Strategy 4: All logic in WebForms - need full extraction
			return ExtractionStrategy.FullExtraction;
		}

		private List<string> ExtractTableAdapters(string content)
		{
			var adapters = new List<string>();
			var matches = Regex.Matches(content, @"(\w+)TableAdapter", RegexOptions.IgnoreCase);
			
			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					adapters.Add(match.Groups[1].Value);
				}
			}

			return adapters.Distinct().ToList();
		}

		private List<string> ExtractDataTables(string content)
		{
			var tables = new List<string>();
			var patterns = new[]
			{
				@"(\w+)DataTable",
				@"dataSet\.(\w+)",
				@"\.(\w+)\.Fill\(",
				@"\.(\w+)\.GetData\("
			};

			foreach (var pattern in patterns)
			{
				var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					if (match.Groups.Count > 1)
					{
						tables.Add(match.Groups[1].Value);
					}
				}
			}

			return tables.Distinct().ToList();
		}

		private List<string> ExtractDatabaseOperations(string content)
		{
			var operations = new List<string>();
			var patterns = new[]
			{
				@"SELECT\s+.*?\s+FROM\s+(\w+)",
				@"INSERT\s+INTO\s+(\w+)",
				@"UPDATE\s+(\w+)\s+SET",
				@"DELETE\s+FROM\s+(\w+)",
				@"ExecuteNonQuery\s*\(\)",
				@"ExecuteScalar\s*\(\)",
				@"ExecuteReader\s*\(\)"
			};

			foreach (var pattern in patterns)
			{
				var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					operations.Add(match.Value.Trim());
				}
			}

			return operations.Distinct().ToList();
		}

		private List<string> ExtractConnectionStrings(string content)
		{
			var connections = new List<string>();
			var matches = Regex.Matches(content, @"ConnectionString[\""\s]*=\s*[\""]([^\""]+)[\""]", RegexOptions.IgnoreCase);
			
			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					connections.Add(match.Groups[1].Value);
				}
			}

			return connections;
		}

		private List<string> ExtractBusinessMethods(string content)
		{
			var methods = new List<string>();
			var patterns = new[]
			{
				@"public\s+(?:async\s+)?(?:\w+\s+)?(\w*(?:Validate|Process|Calculate|Execute|Business)\w*)\s*\(",
				@"private\s+(?:async\s+)?(?:\w+\s+)?(\w*(?:Validate|Process|Calculate|Execute)\w*)\s*\("
			};

			foreach (var pattern in patterns)
			{
				var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					if (match.Groups.Count > 1)
					{
						methods.Add(match.Groups[1].Value);
					}
				}
			}

			return methods.Distinct().ToList();
		}

		private List<string> ExtractValidationMethods(string content)
		{
			var validations = new List<string>();
			var matches = Regex.Matches(content, @"(?:public|private)\s+(?:\w+\s+)?(\w*(?:Valid|Check|Verify)\w*)\s*\(", RegexOptions.IgnoreCase);
			
			foreach (Match match in matches)
			{
				if (match.Groups.Count > 1)
				{
					validations.Add(match.Groups[1].Value);
				}
			}

			return validations.Distinct().ToList();
		}

		private string DetectDataAccessPattern(string content)
		{
			if (content.Contains("DbContext")) return "EntityFramework";
			if (content.Contains("TableAdapter")) return "DataSet/TableAdapter";
			if (content.Contains("SqlConnection")) return "ADO.NET";
			if (content.Contains("Repository")) return "Repository Pattern";
			return "Custom";
		}

		private string DetectBusinessLogicPattern(string content)
		{
			if (Regex.IsMatch(content, @"class\s+\w*Service", RegexOptions.IgnoreCase)) return "Service Layer";
			if (Regex.IsMatch(content, @"class\s+\w*Manager", RegexOptions.IgnoreCase)) return "Manager Pattern";
			if (Regex.IsMatch(content, @"class\s+\w*Business", RegexOptions.IgnoreCase)) return "Business Layer";
			return "Custom";
		}

		private WebFormAnalysis AnalyzeWebFormCodeBehind(string content)
		{
			return new WebFormAnalysis
			{
				HasDataAccess = DetectDataAccessPatterns(content),
				HasBusinessLogic = DetectBusinessLogicPatterns(content),
				ExtractableLogic = ExtractExtractableLogic(content),
				DatabaseOperations = ExtractDatabaseOperations(content),
				BusinessOperations = ExtractBusinessMethods(content)
			};
		}

		private List<string> ExtractExtractableLogic(string content)
		{
			var extractableLogic = new List<string>();
			
			// Find methods in Page_Load that can be extracted
			var pageLoadMatch = Regex.Match(content, @"Page_Load\s*\([^)]*\)\s*{([^}]+)}", RegexOptions.Singleline);
			if (pageLoadMatch.Success)
			{
				var pageLoadContent = pageLoadMatch.Groups[1].Value;
				if (DetectDataAccessPatterns(pageLoadContent))
				{
					extractableLogic.Add("Page_Load - Data Access Logic");
				}
				if (DetectBusinessLogicPatterns(pageLoadContent))
				{
					extractableLogic.Add("Page_Load - Business Logic");
				}
			}

			// Find event handlers with extractable logic
			var eventHandlers = Regex.Matches(content, @"(\w+_(?:Click|Command|SelectedIndexChanged))\s*\([^)]*\)\s*{([^}]+)}", RegexOptions.Singleline);
			foreach (Match handler in eventHandlers)
			{
				if (handler.Groups.Count > 2)
				{
					var handlerName = handler.Groups[1].Value;
					var handlerContent = handler.Groups[2].Value;
					
					if (DetectDataAccessPatterns(handlerContent))
					{
						extractableLogic.Add($"{handlerName} - Data Access Logic");
					}
					if (DetectBusinessLogicPatterns(handlerContent))
					{
						extractableLogic.Add($"{handlerName} - Business Logic");
					}
				}
			}

			return extractableLogic;
		}
	}

	public interface IArchitectureAnalyzer
	{
		Task<ProjectArchitecture> AnalyzeProjectArchitectureAsync(string projectPath);
	}
}