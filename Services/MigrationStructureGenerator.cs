// Updated MigrationStructureGenerator.cs - Fix type conversion issues
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpLegacyMigrationMCP.Models;
using Microsoft.Extensions.Logging;

namespace CSharpLegacyMigrationMCP.Services
{
	public class MigrationStructureGenerator : IMigrationStructureGenerator
	{
		private readonly ILogger<MigrationStructureGenerator> _logger;
		private readonly IDataRepository _repository;

		public MigrationStructureGenerator(ILogger<MigrationStructureGenerator> logger, IDataRepository repository)
		{
			_logger = logger;
			_repository = repository;
		}

		public async Task<MigrationStructure> GenerateStructureAsync(string analysisId)
		{
			try
			{
				_logger.LogInformation($"Generating migration structure for analysis: {analysisId}");

				var codeStructures = await _repository.GetCodeStructuresAsync(analysisId);
				if (!codeStructures.Any())
				{
					throw new MigrationException($"No code structures found for analysis ID: {analysisId}");
				}

				var migrationStructure = new MigrationStructure
				{
					AnalysisId = analysisId
				};

				migrationStructure.DataAccessInterfaces = await GenerateDataAccessInterfacesAsync(codeStructures);

				migrationStructure.BusinessLogicInterfaces = await GenerateBusinessLogicInterfacesAsync(codeStructures);

				migrationStructure.Models = await GenerateModelsAsync(codeStructures);

				migrationStructure.SuggestedProjects = GenerateSuggestedProjects(codeStructures);

				await _repository.SaveMigrationStructureAsync(migrationStructure);

				_logger.LogInformation($"Migration structure generated successfully: {migrationStructure.DataAccessInterfaces.Count} DA interfaces, {migrationStructure.BusinessLogicInterfaces.Count} BL interfaces, {migrationStructure.Models.Count} models");

				return migrationStructure;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error generating migration structure for analysis {analysisId}");
				throw new MigrationException($"Failed to generate migration structure: {ex.Message}", ex);
			}
		}

		public async Task<List<InterfaceStructure>> GenerateDataAccessInterfacesAsync(List<CodeStructure> codeStructures)
		{
			var interfaces = new List<InterfaceStructure>();

			try
			{
				var tableGroups = codeStructures
					.Where(cs => cs.DatabaseTables != null && cs.DatabaseTables.Any() || cs.Type == CodeStructureType.DataAccess)
					.SelectMany(cs => (cs.DatabaseTables ?? new List<string>()).Select(table => new { Table = table, CodeStructure = cs }))
					.Where(x => !string.IsNullOrEmpty(x.Table))
					.GroupBy(x => x.Table, StringComparer.OrdinalIgnoreCase);

				foreach (var group in tableGroups)
				{
					var tableName = group.Key;
					var relatedStructures = group.Select(g => g.CodeStructure).Distinct().ToList();

					var interfaceName = $"I{NormalizeTableName(tableName)}DA";
					var namespaceName = DetermineNamespace(relatedStructures, "DataAccess");

					var interfaceStructure = new InterfaceStructure
					{
						Name = interfaceName,
						Namespace = namespaceName,
						Purpose = $"Data access operations for {tableName} table",
						RelatedTables = new List<string> { tableName }
					};

					interfaceStructure.Methods = GenerateDataAccessMethods(tableName, relatedStructures);

					interfaces.Add(interfaceStructure);
				}

				var unidentifiedDaClasses = codeStructures
					.Where(cs => cs.Methods != null && cs.Methods.Any(m => m.HasDatabaseAccess) &&
							   (cs.DatabaseTables == null || !cs.DatabaseTables.Any()))
					.ToList();

				foreach (var structure in unidentifiedDaClasses)
				{
					var interfaceName = $"I{structure.ClassName?.Replace("Dal", "").Replace("DataAccess", "") ?? "Generic"}DA";
					var namespaceName = DetermineNamespace(new List<CodeStructure> { structure }, "DataAccess");

					var interfaceStructure = new InterfaceStructure
					{
						Name = interfaceName,
						Namespace = namespaceName,
						Purpose = $"Data access operations for {structure.ClassName}",
						RelatedTables = new List<string>()
					};

					interfaceStructure.Methods = GenerateMethodSignaturesFromClass(structure);
					interfaces.Add(interfaceStructure);
				}

				return interfaces;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating data access interfaces");
				throw;
			}
		}

		public async Task<List<InterfaceStructure>> GenerateBusinessLogicInterfacesAsync(List<CodeStructure> codeStructures)
		{
			var interfaces = new List<InterfaceStructure>();

			try
			{
				var businessLogicClasses = codeStructures
					.Where(cs => cs.Type == CodeStructureType.BusinessLogic ||
							   cs.Type == CodeStructureType.WebForm ||
							   IsBusinessLogicClass(cs))
					.ToList();

				var domainGroups = GroupByDomain(businessLogicClasses);

				foreach (var group in domainGroups)
				{
					var domainName = group.Key;
					var relatedStructures = group.Value;

					var interfaceName = $"I{domainName}Business";
					var namespaceName = DetermineNamespace(relatedStructures, "Business");

					var interfaceStructure = new InterfaceStructure
					{
						Name = interfaceName,
						Namespace = namespaceName,
						Purpose = $"Business logic operations for {domainName} domain",
						RelatedTables = ExtractRelatedTables(relatedStructures)
					};

					interfaceStructure.Methods = GenerateBusinessLogicMethods(relatedStructures);
					interfaces.Add(interfaceStructure);
				}

				return interfaces;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating business logic interfaces");
				throw;
			}
		}

		public async Task<List<ModelStructure>> GenerateModelsAsync(List<CodeStructure> codeStructures)
		{
			var models = new List<ModelStructure>();

			try
			{
				var allTables = codeStructures
					.Where(cs => cs.DatabaseTables != null)
					.SelectMany(cs => cs.DatabaseTables)
					.Where(table => !string.IsNullOrEmpty(table))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				foreach (var tableName in allTables)
				{
					var modelName = NormalizeTableName(tableName);
					var namespaceName = DetermineNamespace(codeStructures, "Models");

					var modelStructure = new ModelStructure
					{
						Name = modelName,
						Namespace = namespaceName,
						TableName = tableName,
						Properties = GenerateModelProperties(tableName, codeStructures)
					};

					models.Add(modelStructure);
				}

				var entityClasses = codeStructures
					.Where(cs => cs.Type == CodeStructureType.Model || IsEntityClass(cs))
					.ToList();

				foreach (var entityClass in entityClasses)
				{
					if (models.Any(m => m.Name.Equals(entityClass.ClassName, StringComparison.OrdinalIgnoreCase)))
						continue;

					var modelStructure = new ModelStructure
					{
						Name = entityClass.ClassName ?? "UnknownModel",
						Namespace = DetermineNamespace(new List<CodeStructure> { entityClass }, "Models"),
						TableName = InferTableName(entityClass.ClassName ?? "UnknownModel"),
						Properties = entityClass.Properties?.ToList() ?? new List<PropertyStructure>()
					};

					models.Add(modelStructure);
				}

				return models;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating models");
				throw;
			}
		}

		private bool IsBusinessLogicClass(CodeStructure structure)
		{
			if (structure?.ClassName == null) return false;

			var className = structure.ClassName.ToLower();
			return className.Contains("business") ||
				   className.Contains("logic") ||
				   className.Contains("manager") ||
				   className.Contains("service") ||
				   (structure.Methods?.Any(m => !m.HasDatabaseAccess && m.ComplexityScore > 2) ?? false);
		}

		private bool IsEntityClass(CodeStructure structure)
		{
			if (structure?.Properties == null || structure.Methods == null) return false;

			return structure.Properties.Count > 2 &&
				   structure.Methods.Count < 5 &&
				   structure.Properties.All(p => p.HasGetter && p.HasSetter);
		}

		private Dictionary<string, List<CodeStructure>> GroupByDomain(List<CodeStructure> structures)
		{
			var groups = new Dictionary<string, List<CodeStructure>>();

			foreach (var structure in structures)
			{
				var domain = ExtractDomainFromClassName(structure.ClassName ?? "Unknown");

				if (!groups.ContainsKey(domain))
				{
					groups[domain] = new List<CodeStructure>();
				}

				groups[domain].Add(structure);
			}

			return groups;
		}

		private string ExtractDomainFromClassName(string className)
		{
			if (string.IsNullOrEmpty(className)) return "General";

			var cleanName = className
				.Replace("Page", "")
				.Replace("Form", "")
				.Replace("Business", "")
				.Replace("Logic", "")
				.Replace("Manager", "")
				.Replace("Service", "");

			if (cleanName.Contains("User")) return "User";
			if (cleanName.Contains("Product")) return "Product";
			if (cleanName.Contains("Order")) return "Order";
			if (cleanName.Contains("Customer")) return "Customer";
			if (cleanName.Contains("Employee")) return "Employee";

			var parts = cleanName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 0 ? parts[0] : "General";
		}

		private string NormalizeTableName(string tableName)
		{
			if (string.IsNullOrEmpty(tableName)) return "Unknown";
			return tableName.Replace("_", "").Replace("Tb", "").Replace("Table", "");
		}

		private string DetermineNamespace(List<CodeStructure> structures, string layer)
		{
			var commonNamespace = structures
				.Select(s => s.Namespace)
				.Where(ns => !string.IsNullOrEmpty(ns))
				.GroupBy(ns => ns)
				.OrderByDescending(g => g.Count())
				.FirstOrDefault()?.Key;

			if (!string.IsNullOrEmpty(commonNamespace))
			{
				return $"{commonNamespace}.{layer}";
			}

			return $"YourApp.{layer}";
		}

		private List<string> ExtractRelatedTables(List<CodeStructure> structures)
		{
			return structures
				.Where(s => s.DatabaseTables != null)
				.SelectMany(s => s.DatabaseTables)
				.Where(table => !string.IsNullOrEmpty(table))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private List<string> GenerateDataAccessMethods(string tableName, List<CodeStructure> relatedStructures)
		{
			var methods = new List<string>();
			var modelName = NormalizeTableName(tableName);

			methods.Add($"Task<{modelName}> GetByIdAsync(int id)");
			methods.Add($"Task<IEnumerable<{modelName}>> GetAllAsync()");
			methods.Add($"Task<int> InsertAsync({modelName} entity)");
			methods.Add($"Task<bool> UpdateAsync({modelName} entity)");
			methods.Add($"Task<bool> DeleteAsync(int id)");

			return methods;
		}

		private List<string> GenerateBusinessLogicMethods(List<CodeStructure> relatedStructures)
		{
			var methods = new List<string>();

			foreach (var structure in relatedStructures)
			{
				if (structure.Methods == null) continue;

				foreach (var method in structure.Methods)
				{
					if (!IsPageLifecycleMethod(method.Name) && IsBusinessMethod(method))
					{
						var signature = GenerateBusinessMethodSignature(method);
						if (!methods.Contains(signature))
						{
							methods.Add(signature);
						}
					}
				}
			}

			return methods;
		}

		private List<string> GenerateMethodSignaturesFromClass(CodeStructure structure)
		{
			if (structure.Methods == null) return new List<string>();

			return structure.Methods
				.Where(m => m.HasDatabaseAccess)
				.Select(m => GenerateMethodSignature(m, "object"))
				.ToList();
		}

		private List<PropertyStructure> GenerateModelProperties(string tableName, List<CodeStructure> codeStructures)
		{
			var properties = new List<PropertyStructure>();

			properties.Add(new PropertyStructure
			{
				Name = "Id",
				Type = "int",
				HasGetter = true,
				HasSetter = true,
				IsPublic = true
			});

			return properties;
		}

		private List<string> GenerateSuggestedProjects(List<CodeStructure> codeStructures)
		{
			var projects = new List<string>();

			var hasWebForms = codeStructures.Any(cs => cs.Type == CodeStructureType.WebForm);
			var hasDataAccess = codeStructures.Any(cs => cs.Type == CodeStructureType.DataAccess ||
														 (cs.Methods?.Any(m => m.HasDatabaseAccess) ?? false));
			var hasBusinessLogic = codeStructures.Any(cs => cs.Type == CodeStructureType.BusinessLogic || IsBusinessLogicClass(cs));

			projects.Add("YourApp.Models - Contains all entity/model classes");

			if (hasDataAccess)
			{
				projects.Add("YourApp.DataAccess - Contains data access interfaces and implementations");
			}

			if (hasBusinessLogic)
			{
				projects.Add("YourApp.Business - Contains business logic interfaces and implementations");
			}

			if (hasWebForms)
			{
				projects.Add("YourApp.Web - Contains migrated web forms and pages");
			}

			projects.Add("YourApp.Common - Contains shared utilities and extensions");

			return projects;
		}

		private bool IsPageLifecycleMethod(string methodName)
		{
			if (string.IsNullOrEmpty(methodName)) return false;
			var lifecycleMethods = new[] { "Page_Load", "Page_Init", "Page_PreRender", "Page_Unload" };
			return lifecycleMethods.Contains(methodName);
		}

		private bool IsBusinessMethod(MethodStructure method)
		{
			return method.ComplexityScore > 1 &&
				   !method.HasDatabaseAccess &&
				   !IsPageLifecycleMethod(method.Name);
		}

		private string GenerateMethodSignature(MethodStructure method, string contextType)
		{
			var paramString = string.Join(", ", method.Parameters?.Select(p => $"{p.Type} {p.Name}") ?? new string[0]);

			if (method.ReturnType == "void")
			{
				return $"Task {method.Name}Async({paramString})";
			}
			else
			{
				return $"Task<{method.ReturnType}> {method.Name}Async({paramString})";
			}
		}

		private string GenerateBusinessMethodSignature(MethodStructure method)
		{
			var paramString = string.Join(", ", method.Parameters?
				.Where(p => !IsSystemParameter(p.Name))
				.Select(p => $"{p.Type} {p.Name}") ?? new string[0]);

			if (method.ReturnType == "void")
			{
				return $"Task {method.Name}Async({paramString})";
			}
			else
			{
				return $"Task<{method.ReturnType}> {method.Name}Async({paramString})";
			}
		}

		private bool IsSystemParameter(string paramName)
		{
			if (string.IsNullOrEmpty(paramName)) return true;
			var systemParams = new[] { "sender", "e", "args", "context", "request", "response" };
			return systemParams.Contains(paramName.ToLower());
		}

		private string InferTableName(string className)
		{
			return className + "Tb";
		}
	}
}