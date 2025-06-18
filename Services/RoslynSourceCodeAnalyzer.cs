using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSharpLegacyMigrationMCP;
using CSharpLegacyMigrationMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CSharpLegacyMigrationMCP.Services
{
	public class RoslynSourceCodeAnalyzer : ISourceCodeAnalyzer
	{
		private readonly ILogger<RoslynSourceCodeAnalyzer> _logger;
		private readonly IDataRepository _repository;

		public RoslynSourceCodeAnalyzer(ILogger<RoslynSourceCodeAnalyzer> logger, IDataRepository repository)
		{
			_logger = logger;
			_repository = repository;
		}

		public async Task<AnalysisResult> AnalyzeDirectoryAsync(string directoryPath, bool includeSubdirectories = true)
		{
			try
			{
				_logger.LogInformation($"Starting analysis of directory: {directoryPath}");

				if (!Directory.Exists(directoryPath))
				{
					throw new AnalysisException($"Directory not found: {directoryPath}");
				}

				var analysisResult = new AnalysisResult
				{
					DirectoryPath = directoryPath
				};

				var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
				var csharpFiles = Directory.GetFiles(directoryPath, "*.cs", searchOption)
					.Concat(Directory.GetFiles(directoryPath, "*.aspx.cs", searchOption))
					.Concat(Directory.GetFiles(directoryPath, "*.ascx.cs", searchOption))
					.ToList();

				var aspxFiles = Directory.GetFiles(directoryPath, "*.aspx", searchOption)
					.Concat(Directory.GetFiles(directoryPath, "*.ascx", searchOption))
					.ToList();

				analysisResult.TotalFiles = csharpFiles.Count + aspxFiles.Count;

				_logger.LogInformation($"Found {csharpFiles.Count} C# files and {aspxFiles.Count} ASPX files");

				// Analyze C# files
				foreach (var filePath in csharpFiles)
				{
					try
					{
						var codeStructure = await AnalyzeFileAsync(filePath);
						if (codeStructure != null)
						{
							codeStructure.AnalysisId = analysisResult.Id;
							analysisResult.CodeStructures.Add(codeStructure);
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning($"Failed to analyze file {filePath}: {ex.Message}");
					}
				}

				// Analyze ASPX files for additional context
				foreach (var aspxFile in aspxFiles)
				{
					try
					{
						await AnalyzeAspxFileAsync(aspxFile, analysisResult);
					}
					catch (Exception ex)
					{
						_logger.LogWarning($"Failed to analyze ASPX file {aspxFile}: {ex.Message}");
					}
				}

				// Calculate summary statistics
				analysisResult.TotalClasses = analysisResult.CodeStructures.Count;
				analysisResult.TotalMethods = analysisResult.CodeStructures.Sum(cs => cs.Methods?.Count ?? 0);
				analysisResult.ComplexityScore = CalculateOverallComplexity(analysisResult.CodeStructures);

				// Extract dependencies
				analysisResult.Dependencies = ExtractDependencies(analysisResult.CodeStructures);

				// Save to database
				await _repository.SaveAnalysisResultAsync(analysisResult);

				_logger.LogInformation($"Analysis completed. Found {analysisResult.TotalClasses} classes with {analysisResult.TotalMethods} methods");

				return analysisResult;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing directory {directoryPath}");
				throw new AnalysisException($"Failed to analyze directory: {ex.Message}", ex);
			}
		}

		public async Task<CodeStructure> AnalyzeFileAsync(string filePath)
		{
			try
			{
				var sourceCode = await File.ReadAllTextAsync(filePath);
				var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
				var root = syntaxTree.GetRoot();

				var codeStructure = new CodeStructure
				{
					FilePath = filePath,
					SourceCode = sourceCode,
					Type = DetermineCodeStructureType(filePath, sourceCode)
				};

				var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
				if (namespaceDeclaration != null)
				{
					codeStructure.Namespace = namespaceDeclaration.Name.ToString();
				}

				var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
				if (classDeclaration != null)
				{
					codeStructure.ClassName = classDeclaration.Identifier.Text;

					codeStructure.Methods = ExtractMethods(classDeclaration, sourceCode);

					codeStructure.Properties = ExtractProperties(classDeclaration);

					codeStructure.ComplexityScore = CalculateClassComplexity(classDeclaration);

					codeStructure.Dependencies = ExtractClassDependencies(root);

					codeStructure.DatabaseTables = ExtractDatabaseTables(sourceCode);
				}

				return codeStructure;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error analyzing file {filePath}");
				return null;
			}
		}

		private async Task AnalyzeAspxFileAsync(string aspxFile, AnalysisResult analysisResult)
		{
			try
			{
				var aspxContent = await File.ReadAllTextAsync(aspxFile);

				var codeBehindFile = aspxFile + ".cs";
				if (!File.Exists(codeBehindFile))
				{
					codeBehindFile = aspxFile.Replace(".aspx", ".aspx.cs").Replace(".ascx", ".ascx.cs");
				}

				if (File.Exists(codeBehindFile))
				{
					var codeStructure = analysisResult.CodeStructures.FirstOrDefault(cs => cs.FilePath == codeBehindFile);
					if (codeStructure != null)
					{
						var controls = ExtractControlsFromAspx(aspxContent);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Error analyzing ASPX file {aspxFile}: {ex.Message}");
			}
		}

		private CodeStructureType DetermineCodeStructureType(string filePath, string sourceCode)
		{
			var fileName = Path.GetFileName(filePath).ToLower();

			if (fileName.EndsWith(".aspx.cs"))
				return CodeStructureType.WebForm;
			if (fileName.EndsWith(".ascx.cs"))
				return CodeStructureType.UserControl;
			if (sourceCode.Contains("System.Web.UI.Page") || sourceCode.Contains(": Page"))
				return CodeStructureType.WebForm;
			if (sourceCode.Contains("System.Web.UI.UserControl") || sourceCode.Contains(": UserControl"))
				return CodeStructureType.UserControl;
			if (sourceCode.Contains("SqlConnection") || sourceCode.Contains("SqlCommand") ||
				sourceCode.Contains("DataSet") || sourceCode.Contains("SqlDataAdapter"))
				return CodeStructureType.DataAccess;
			if (fileName.Contains("business") || fileName.Contains("logic") || fileName.Contains("service"))
				return CodeStructureType.BusinessLogic;
			if (fileName.Contains("model") || fileName.Contains("entity"))
				return CodeStructureType.Model;
			if (fileName.Contains("util") || fileName.Contains("helper"))
				return CodeStructureType.Utility;

			return CodeStructureType.Unknown;
		}

		private List<MethodStructure> ExtractMethods(ClassDeclarationSyntax classDeclaration, string sourceCode)
		{
			var methods = new List<MethodStructure>();

			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				var methodStructure = new MethodStructure
				{
					Name = method.Identifier.Text,
					ReturnType = method.ReturnType.ToString(),
					SourceCode = method.ToString(),
					LinesOfCode = method.ToString().Split('\n').Length,
					ComplexityScore = CalculateMethodComplexity(method),
					Parameters = ExtractParameters(method),
					Dependencies = ExtractMethodDependencies(method),
					HasDatabaseAccess = HasDatabaseAccess(method.ToString()),
					DatabaseOperations = ExtractDatabaseOperations(method.ToString())
				};

				methods.Add(methodStructure);
			}

			return methods;
		}

		private List<ParameterStructure> ExtractParameters(MethodDeclarationSyntax method)
		{
			var parameters = new List<ParameterStructure>();

			foreach (var param in method.ParameterList.Parameters)
			{
				var paramStructure = new ParameterStructure
				{
					Name = param.Identifier.Text,
					Type = param.Type?.ToString() ?? "object",
					IsOptional = param.Default != null,
					DefaultValue = param.Default?.Value?.ToString()
				};

				parameters.Add(paramStructure);
			}

			return parameters;
		}

		private List<PropertyStructure> ExtractProperties(ClassDeclarationSyntax classDeclaration)
		{
			var properties = new List<PropertyStructure>();

			foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
			{
				var propStructure = new PropertyStructure
				{
					Name = property.Identifier.Text,
					Type = property.Type.ToString(),
					HasGetter = property.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.GetKeyword)) ?? false,
					HasSetter = property.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) ?? false,
					IsPublic = property.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
				};

				properties.Add(propStructure);
			}

			return properties;
		}

		private double CalculateClassComplexity(ClassDeclarationSyntax classDeclaration)
		{
			double complexity = 1;

			foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
			{
				complexity += CalculateMethodComplexity(method);
			}

			complexity += classDeclaration.Members.OfType<ClassDeclarationSyntax>().Count() * 2;
			complexity += classDeclaration.Members.OfType<InterfaceDeclarationSyntax>().Count() * 1.5;

			return complexity;
		}

		private double CalculateMethodComplexity(MethodDeclarationSyntax method)
		{
			double complexity = 1;

			var body = method.Body;
			if (body != null)
			{
				complexity += body.DescendantNodes().OfType<IfStatementSyntax>().Count();
				complexity += body.DescendantNodes().OfType<WhileStatementSyntax>().Count();
				complexity += body.DescendantNodes().OfType<ForStatementSyntax>().Count();
				complexity += body.DescendantNodes().OfType<ForEachStatementSyntax>().Count();
				complexity += body.DescendantNodes().OfType<SwitchStatementSyntax>().Count();
				complexity += body.DescendantNodes().OfType<CatchClauseSyntax>().Count();

				foreach (var switchStmt in body.DescendantNodes().OfType<SwitchStatementSyntax>())
				{
					complexity += switchStmt.Sections.Sum(s => s.Labels.Count) - 1;
				}
			}

			return complexity;
		}

		private List<string> ExtractClassDependencies(SyntaxNode root)
		{
			var dependencies = new HashSet<string>();

			foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
			{
				dependencies.Add(usingDirective.Name.ToString());
			}

			foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				var name = identifier.Identifier.Text;
				if (char.IsUpper(name[0]) && !IsBuiltInType(name))
				{
					dependencies.Add(name);
				}
			}

			return dependencies.ToList();
		}

		private List<string> ExtractMethodDependencies(MethodDeclarationSyntax method)
		{
			var dependencies = new HashSet<string>();

			foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
			{
				var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
				if (memberAccess != null)
				{
					dependencies.Add(memberAccess.Name.Identifier.Text);
				}
			}

			foreach (var objectCreation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
			{
				dependencies.Add(objectCreation.Type.ToString());
			}

			return dependencies.ToList();
		}

		private bool HasDatabaseAccess(string methodCode)
		{
			var dbKeywords = new[]
			{
				"SqlConnection", "SqlCommand", "SqlDataAdapter", "SqlDataReader",
				"OleDbConnection", "OleDbCommand", "OdbcConnection", "OdbcCommand",
				"DataSet", "DataTable", "DataRow", "ExecuteNonQuery", "ExecuteScalar",
				"ExecuteReader", "Fill", "Update", "SELECT", "INSERT", "UPDATE", "DELETE"
			};

			return dbKeywords.Any(keyword => methodCode.Contains(keyword));
		}

		private List<string> ExtractDatabaseOperations(string methodCode)
		{
			var operations = new List<string>();

			if (methodCode.Contains("ExecuteNonQuery") || Regex.IsMatch(methodCode, @"\b(INSERT|UPDATE|DELETE)\b", RegexOptions.IgnoreCase))
				operations.Add("Write");

			if (methodCode.Contains("ExecuteReader") || methodCode.Contains("Fill") || Regex.IsMatch(methodCode, @"\bSELECT\b", RegexOptions.IgnoreCase))
				operations.Add("Read");

			if (methodCode.Contains("ExecuteScalar"))
				operations.Add("Scalar");

			return operations.Distinct().ToList();
		}

		private List<string> ExtractDatabaseTables(string sourceCode)
		{
			var tables = new HashSet<string>();

			var sqlQueries = ExtractSqlQueries(sourceCode);
			foreach (var query in sqlQueries)
			{
				var tableMatches = Regex.Matches(query, @"\b(?:FROM|JOIN|INTO|UPDATE)\s+(\w+)", RegexOptions.IgnoreCase);
				foreach (Match match in tableMatches)
				{
					if (match.Groups.Count > 1)
					{
						tables.Add(match.Groups[1].Value);
					}
				}
			}

			return tables.ToList();
		}

		private List<string> ExtractSqlQueries(string sourceCode)
		{
			var queries = new List<string>();

			var stringMatches = Regex.Matches(sourceCode, @"""([^""]*(?:SELECT|INSERT|UPDATE|DELETE)[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			foreach (Match match in stringMatches)
			{
				if (match.Groups.Count > 1)
				{
					queries.Add(match.Groups[1].Value);
				}
			}

			return queries;
		}

		private List<string> ExtractControlsFromAspx(string aspxContent)
		{
			var controls = new List<string>();

			var controlMatches = Regex.Matches(aspxContent, @"<asp:(\w+)[^>]*runat=""?server""?[^>]*>", RegexOptions.IgnoreCase);
			foreach (Match match in controlMatches)
			{
				if (match.Groups.Count > 1)
				{
					controls.Add(match.Groups[1].Value);
				}
			}

			return controls;
		}

		private double CalculateOverallComplexity(List<CodeStructure> codeStructures)
		{
			if (!codeStructures.Any())
				return 0;

			return codeStructures.Average(cs => cs.ComplexityScore);
		}

		private List<string> ExtractDependencies(List<CodeStructure> codeStructures)
		{
			var allDependencies = new HashSet<string>();

			foreach (var structure in codeStructures)
			{
				foreach (var dependency in structure.Dependencies)
				{
					allDependencies.Add(dependency);
				}
			}

			return allDependencies.ToList();
		}

		private bool IsBuiltInType(string typeName)
		{
			var builtInTypes = new[]
			{
				"string", "int", "double", "float", "bool", "decimal", "DateTime",
				"object", "void", "byte", "char", "short", "long", "uint", "ulong",
				"ushort", "sbyte", "String", "Int32", "Double", "Single", "Boolean",
				"Decimal", "Object", "Void", "Byte", "Char", "Int16", "Int64"
			};

			return builtInTypes.Contains(typeName);
		}
	}
}