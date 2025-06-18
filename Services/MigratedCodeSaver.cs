using CSharpLegacyMigrationMCP.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLegacyMigrationMCP.Services
{
	public class MigratedCodeSaver : IMigratedCodeSaver
	{
		private readonly ILogger<MigratedCodeSaver> _logger;
		private readonly IDataRepository _repository;

		public MigratedCodeSaver(ILogger<MigratedCodeSaver> logger, IDataRepository repository)
		{
			_logger = logger;
			_repository = repository;
		}

		public async Task<SaveResult> SaveMigratedCodeAsync(string analysisId, string outputDirectory)
		{
			try
			{
				_logger.LogInformation($"Saving migrated code for analysis {analysisId} to {outputDirectory}");

				var result = new SaveResult
				{
					OutputDirectory = outputDirectory
				};

				Directory.CreateDirectory(outputDirectory);

				var migrationStructure = await _repository.GetMigrationStructureAsync(analysisId);
				var codeChunks = await _repository.GetCodeChunksAsync(analysisId);
				var processedChunks = codeChunks.Where(c => c.IsProcessed && !string.IsNullOrEmpty(c.MigratedCode)).ToList();

				var projectDirs = CreateProjectStructure(outputDirectory, migrationStructure);

				await SaveInterfacesAsync(projectDirs["Interfaces"], migrationStructure);

				await SaveModelsAsync(projectDirs["Models"], migrationStructure);

				await SaveMigratedCodeChunksAsync(projectDirs, processedChunks);

				await GenerateSampleImplementationsAsync(projectDirs, migrationStructure);

				result.WebFormGenerated = await GenerateSampleWebFormAsync(outputDirectory, migrationStructure);

				await GenerateProjectFilesAsync(outputDirectory, migrationStructure);

				result.FilesSaved = CountGeneratedFiles(outputDirectory);
				result.GeneratedFiles = GetGeneratedFilesList(outputDirectory);

				_logger.LogInformation($"Successfully saved {result.FilesSaved} migrated files to {outputDirectory}");

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error saving migrated code for analysis {analysisId}");
				throw new MigrationException($"Failed to save migrated code: {ex.Message}", ex);
			}
		}

		public async Task<bool> GenerateSampleWebFormAsync(string outputDirectory, MigrationStructure structure)
		{
			try
			{
				var webFormsDir = Path.Combine(outputDirectory, "Web");
				Directory.CreateDirectory(webFormsDir);

				var aspxContent = GenerateSampleAspxContent(structure);
				var codeBehindContent = GenerateSampleCodeBehindContent(structure);

				await File.WriteAllTextAsync(Path.Combine(webFormsDir, "Sample.aspx"), aspxContent);
				await File.WriteAllTextAsync(Path.Combine(webFormsDir, "Sample.aspx.cs"), codeBehindContent);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating sample web form");
				return false;
			}
		}

		private Dictionary<string, string> CreateProjectStructure(string outputDirectory, MigrationStructure structure)
		{
			var projectDirs = new Dictionary<string, string>();

			foreach (var project in structure.SuggestedProjects)
			{
				var projectName = project.Split('-')[0].Trim();
				var projectDir = Path.Combine(outputDirectory, projectName);
				Directory.CreateDirectory(projectDir);

				if (project.Contains("Interfaces"))
					projectDirs["Interfaces"] = projectDir;
				else if (project.Contains("Models"))
					projectDirs["Models"] = projectDir;
				else if (project.Contains("DataAccess"))
					projectDirs["DataAccess"] = projectDir;
				else if (project.Contains("Business"))
					projectDirs["Business"] = projectDir;
				else if (project.Contains("Web"))
					projectDirs["Web"] = projectDir;
				else if (project.Contains("Common"))
					projectDirs["Common"] = projectDir;
			}

			if (!projectDirs.ContainsKey("Interfaces"))
				projectDirs["Interfaces"] = Path.Combine(outputDirectory, "Interfaces");
			if (!projectDirs.ContainsKey("Models"))
				projectDirs["Models"] = Path.Combine(outputDirectory, "Models");
			if (!projectDirs.ContainsKey("DataAccess"))
				projectDirs["DataAccess"] = Path.Combine(outputDirectory, "DataAccess");
			if (!projectDirs.ContainsKey("Business"))
				projectDirs["Business"] = Path.Combine(outputDirectory, "Business");

			foreach (var dir in projectDirs.Values)
			{
				Directory.CreateDirectory(dir);
			}

			return projectDirs;
		}

		private async Task SaveInterfacesAsync(string interfacesDir, MigrationStructure structure)
		{
			foreach (var daInterface in structure.DataAccessInterfaces)
			{
				var content = GenerateInterfaceCode(daInterface, "DataAccess");
				var fileName = $"{daInterface.Name}.cs";
				await File.WriteAllTextAsync(Path.Combine(interfacesDir, fileName), content);
			}

			foreach (var blInterface in structure.BusinessLogicInterfaces)
			{
				var content = GenerateInterfaceCode(blInterface, "Business");
				var fileName = $"{blInterface.Name}.cs";
				await File.WriteAllTextAsync(Path.Combine(interfacesDir, fileName), content);
			}
		}

		private async Task SaveModelsAsync(string modelsDir, MigrationStructure structure)
		{
			foreach (var model in structure.Models)
			{
				var content = GenerateModelCode(model);
				var fileName = $"{model.Name}.cs";
				await File.WriteAllTextAsync(Path.Combine(modelsDir, fileName), content);
			}
		}

		private async Task SaveMigratedCodeChunksAsync(Dictionary<string, string> projectDirs, List<CodeChunk> processedChunks)
		{
			foreach (var chunk in processedChunks)
			{
				try
				{
					var parsedCode = ParseMigratedCode(chunk.MigratedCode);

					foreach (var codeFile in parsedCode)
					{
						var targetDir = DetermineTargetDirectory(codeFile.Type, projectDirs);
						var filePath = Path.Combine(targetDir, codeFile.FileName);
						await File.WriteAllTextAsync(filePath, codeFile.Content);
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning($"Failed to save migrated code chunk {chunk.Id}: {ex.Message}");
				}
			}
		}

		private async Task GenerateSampleImplementationsAsync(Dictionary<string, string> projectDirs, MigrationStructure structure)
		{
			foreach (var daInterface in structure.DataAccessInterfaces)
			{
				var content = GenerateDataAccessImplementation(daInterface);
				var fileName = daInterface.Name.Replace("I", "") + ".cs";
				await File.WriteAllTextAsync(Path.Combine(projectDirs["DataAccess"], fileName), content);
			}

			foreach (var blInterface in structure.BusinessLogicInterfaces)
			{
				var content = GenerateBusinessLogicImplementation(blInterface, structure.DataAccessInterfaces);
				var fileName = blInterface.Name.Replace("I", "") + ".cs";
				await File.WriteAllTextAsync(Path.Combine(projectDirs["Business"], fileName), content);
			}
		}

		private async Task GenerateProjectFilesAsync(string outputDirectory, MigrationStructure structure)
		{
			foreach (var project in structure.SuggestedProjects)
			{
				var projectName = project.Split('-')[0].Trim();
				var projectDir = Path.Combine(outputDirectory, projectName);

				if (Directory.Exists(projectDir))
				{
					var csprojContent = GenerateProjectFileContent(projectName, project);
					await File.WriteAllTextAsync(Path.Combine(projectDir, $"{projectName}.csproj"), csprojContent);
				}
			}

			var solutionContent = GenerateSolutionFileContent(structure.SuggestedProjects);
			await File.WriteAllTextAsync(Path.Combine(outputDirectory, "MigratedSolution.sln"), solutionContent);
		}

		private string GenerateInterfaceCode(InterfaceStructure interfaceStructure, string layer)
		{
			var sb = new StringBuilder();

			sb.AppendLine("using System;");
			sb.AppendLine("using System.Collections.Generic;");
			sb.AppendLine("using System.Threading.Tasks;");
			sb.AppendLine();
			sb.AppendLine($"namespace {interfaceStructure.Namespace}");
			sb.AppendLine("{");
			sb.AppendLine($"    /// <summary>");
			sb.AppendLine($"    /// {interfaceStructure.Purpose}");
			sb.AppendLine($"    /// </summary>");
			sb.AppendLine($"    public interface {interfaceStructure.Name}");
			sb.AppendLine("    {");

			foreach (var method in interfaceStructure.Methods)
			{
				sb.AppendLine($"        /// <summary>");
				sb.AppendLine($"        /// {method}");
				sb.AppendLine($"        /// </summary>");
				sb.AppendLine($"        {method};");
				sb.AppendLine();
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private string GenerateModelCode(ModelStructure model)
		{
			var sb = new StringBuilder();

			sb.AppendLine("using System;");
			sb.AppendLine("using System.ComponentModel.DataAnnotations;");
			sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
			sb.AppendLine();
			sb.AppendLine($"namespace {model.Namespace}");
			sb.AppendLine("{");
			sb.AppendLine($"    /// <summary>");
			sb.AppendLine($"    /// Entity model for {model.TableName} table");
			sb.AppendLine($"    /// </summary>");
			sb.AppendLine($"    [Table(\"{model.TableName}\")]");
			sb.AppendLine($"    public class {model.Name}");
			sb.AppendLine("    {");

			foreach (var property in model.Properties)
			{
				if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
				{
					sb.AppendLine("        [Key]");
				}

				sb.AppendLine($"        public {property.Type} {property.Name} {{ get; set; }}");
				sb.AppendLine();
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private string GenerateDataAccessImplementation(InterfaceStructure daInterface)
		{
			var sb = new StringBuilder();
			var className = daInterface.Name.Replace("I", "");

			sb.AppendLine("using System;");
			sb.AppendLine("using System.Collections.Generic;");
			sb.AppendLine("using System.Threading.Tasks;");
			sb.AppendLine("using Microsoft.Extensions.Logging;");
			sb.AppendLine();
			sb.AppendLine($"namespace {daInterface.Namespace.Replace("Interfaces", "Implementations")}");
			sb.AppendLine("{");
			sb.AppendLine($"    public class {className} : {daInterface.Name}");
			sb.AppendLine("    {");
			sb.AppendLine($"        private readonly ILogger<{className}> _logger;");
			sb.AppendLine("        private readonly string _connectionString;");
			sb.AppendLine();
			sb.AppendLine($"        public {className}(ILogger<{className}> logger, string connectionString)");
			sb.AppendLine("        {");
			sb.AppendLine("            _logger = logger;");
			sb.AppendLine("            _connectionString = connectionString;");
			sb.AppendLine("        }");
			sb.AppendLine();

			foreach (var method in daInterface.Methods)
			{
				sb.AppendLine($"        public {method}");
				sb.AppendLine("        {");
				sb.AppendLine("            // TODO: Implement database operation");
				sb.AppendLine("            throw new NotImplementedException();");
				sb.AppendLine("        }");
				sb.AppendLine();
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private string GenerateBusinessLogicImplementation(InterfaceStructure blInterface, List<InterfaceStructure> dataAccessInterfaces)
		{
			var sb = new StringBuilder();
			var className = blInterface.Name.Replace("I", "");

			sb.AppendLine("using System;");
			sb.AppendLine("using System.Collections.Generic;");
			sb.AppendLine("using System.Threading.Tasks;");
			sb.AppendLine("using Microsoft.Extensions.Logging;");
			sb.AppendLine();
			sb.AppendLine($"namespace {blInterface.Namespace.Replace("Interfaces", "Implementations")}");
			sb.AppendLine("{");
			sb.AppendLine($"    public class {className} : {blInterface.Name}");
			sb.AppendLine("    {");
			sb.AppendLine($"        private readonly ILogger<{className}> _logger;");

			foreach (var table in blInterface.RelatedTables)
			{
				var relatedDa = dataAccessInterfaces.FirstOrDefault(da => da.RelatedTables.Contains(table));
				if (relatedDa != null)
				{
					sb.AppendLine($"        private readonly {relatedDa.Name} _{ToCamelCase(relatedDa.Name.Replace("I", ""))}DA;");
				}
			}

			sb.AppendLine();
			sb.AppendLine($"        public {className}(ILogger<{className}> logger");

			foreach (var table in blInterface.RelatedTables)
			{
				var relatedDa = dataAccessInterfaces.FirstOrDefault(da => da.RelatedTables.Contains(table));
				if (relatedDa != null)
				{
					sb.AppendLine($"            , {relatedDa.Name} {ToCamelCase(relatedDa.Name.Replace("I", ""))}DA");
				}
			}

			sb.AppendLine("        )");
			sb.AppendLine("        {");
			sb.AppendLine("            _logger = logger;");

			foreach (var table in blInterface.RelatedTables)
			{
				var relatedDa = dataAccessInterfaces.FirstOrDefault(da => da.RelatedTables.Contains(table));
				if (relatedDa != null)
				{
					var fieldName = ToCamelCase(relatedDa.Name.Replace("I", ""));
					sb.AppendLine($"            _{fieldName}DA = {fieldName}DA;");
				}
			}

			sb.AppendLine("        }");
			sb.AppendLine();

			foreach (var method in blInterface.Methods)
			{
				sb.AppendLine($"        public {method}");
				sb.AppendLine("        {");
				sb.AppendLine("            // TODO: Implement business logic");
				sb.AppendLine("            throw new NotImplementedException();");
				sb.AppendLine("        }");
				sb.AppendLine();
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private string GenerateSampleAspxContent(MigrationStructure structure)
		{
			var sb = new StringBuilder();

			sb.AppendLine("<%@ Page Language=\"C#\" AutoEventWireup=\"true\" CodeBehind=\"Sample.aspx.cs\" Inherits=\"YourApp.Web.Sample\" %>");
			sb.AppendLine();
			sb.AppendLine("<!DOCTYPE html>");
			sb.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
			sb.AppendLine("<head runat=\"server\">");
			sb.AppendLine("    <title>Sample Migrated Web Form</title>");
			sb.AppendLine("    <style>");
			sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
			sb.AppendLine("        .container { max-width: 800px; margin: 0 auto; }");
			sb.AppendLine("        .form-group { margin-bottom: 15px; }");
			sb.AppendLine("        .btn { padding: 8px 16px; margin-right: 10px; }");
			sb.AppendLine("    </style>");
			sb.AppendLine("</head>");
			sb.AppendLine("<body>");
			sb.AppendLine("    <form id=\"form1\" runat=\"server\">");
			sb.AppendLine("        <div class=\"container\">");
			sb.AppendLine("            <h2>Sample Migrated Web Form</h2>");
			sb.AppendLine("            <p>This demonstrates the migrated architecture with dependency injection.</p>");
			sb.AppendLine("            ");
			sb.AppendLine("            <div class=\"form-group\">");
			sb.AppendLine("                <asp:Label runat=\"server\" Text=\"Sample Data:\"></asp:Label>");
			sb.AppendLine("                <asp:GridView ID=\"GridView1\" runat=\"server\" AutoGenerateColumns=\"true\" CssClass=\"table\"></asp:GridView>");
			sb.AppendLine("            </div>");
			sb.AppendLine("            ");
			sb.AppendLine("            <div class=\"form-group\">");
			sb.AppendLine("                <asp:Button ID=\"LoadDataButton\" runat=\"server\" Text=\"Load Data\" OnClick=\"LoadDataButton_Click\" CssClass=\"btn\" />");
			sb.AppendLine("                <asp:Label ID=\"StatusLabel\" runat=\"server\" Text=\"\"></asp:Label>");
			sb.AppendLine("            </div>");
			sb.AppendLine("        </div>");
			sb.AppendLine("    </form>");
			sb.AppendLine("</body>");
			sb.AppendLine("</html>");

			return sb.ToString();
		}

		private string GenerateSampleCodeBehindContent(MigrationStructure structure)
		{
			var sb = new StringBuilder();

			sb.AppendLine("using System;");
			sb.AppendLine("using System.Threading.Tasks;");
			sb.AppendLine("using System.Web.UI;");
			sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
			sb.AppendLine("using Microsoft.Extensions.Logging;");
			sb.AppendLine();
			sb.AppendLine("namespace YourApp.Web");
			sb.AppendLine("{");
			sb.AppendLine("    public partial class Sample : Page");
			sb.AppendLine("    {");
			sb.AppendLine("        private readonly ILogger<Sample> _logger;");

			foreach (var blInterface in structure.BusinessLogicInterfaces.Take(2))
			{
				sb.AppendLine($"        private readonly {blInterface.Name} _{ToCamelCase(blInterface.Name.Replace("I", ""))}Business;");
			}

			sb.AppendLine();
			sb.AppendLine("        public Sample()");
			sb.AppendLine("        {");
			sb.AppendLine("            // In a real application, use dependency injection container");
			sb.AppendLine("            // This is just a demonstration");
			sb.AppendLine("        }");
			sb.AppendLine();
			sb.AppendLine("        protected void Page_Load(object sender, EventArgs e)");
			sb.AppendLine("        {");
			sb.AppendLine("            if (!IsPostBack)");
			sb.AppendLine("            {");
			sb.AppendLine("                StatusLabel.Text = \"Page loaded using migrated architecture\";");
			sb.AppendLine("            }");
			sb.AppendLine("        }");
			sb.AppendLine();
			sb.AppendLine("        protected async void LoadDataButton_Click(object sender, EventArgs e)");
			sb.AppendLine("        {");
			sb.AppendLine("            try");
			sb.AppendLine("            {");
			sb.AppendLine("                StatusLabel.Text = \"Loading data using business logic layer...\";");
			sb.AppendLine("                ");
			sb.AppendLine("                // TODO: Use injected business logic services");
			sb.AppendLine("                // var data = await _userBusiness.GetAllUsersAsync();");
			sb.AppendLine("                // GridView1.DataSource = data;");
			sb.AppendLine("                // GridView1.DataBind();");
			sb.AppendLine("                ");
			sb.AppendLine("                StatusLabel.Text = \"Data loaded successfully!\";");
			sb.AppendLine("            }");
			sb.AppendLine("            catch (Exception ex)");
			sb.AppendLine("            {");
			sb.AppendLine("                StatusLabel.Text = $\"Error: {ex.Message}\";");
			sb.AppendLine("                _logger?.LogError(ex, \"Error loading data\");");
			sb.AppendLine("            }");
			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private List<CodeFile> ParseMigratedCode(string migratedCode)
		{
			var files = new List<CodeFile>();

			var sections = migratedCode.Split("// === FILE:", StringSplitOptions.RemoveEmptyEntries);

			foreach (var section in sections.Skip(1))
			{
				var lines = section.Split('\n');
				if (lines.Length > 0)
				{
					var header = lines[0].Trim();
					var parts = header.Split(' ');

					if (parts.Length >= 2)
					{
						var fileName = parts[0];
						var fileType = parts.Length > 2 ? parts[2] : "Implementation";
						var content = string.Join("\n", lines.Skip(1));

						files.Add(new CodeFile
						{
							FileName = fileName,
							Type = fileType,
							Content = content.Trim()
						});
					}
				}
			}

			if (!files.Any())
			{
				files.Add(new CodeFile
				{
					FileName = "MigratedCode.cs",
					Type = "Implementation",
					Content = migratedCode
				});
			}

			return files;
		}

		private string DetermineTargetDirectory(string fileType, Dictionary<string, string> projectDirs)
		{
			switch (fileType.ToLower())
			{
				case "interface":
				case "interfaces":
					return projectDirs.GetValueOrDefault("Interfaces", projectDirs.Values.First());
				case "model":
				case "models":
				case "entity":
					return projectDirs.GetValueOrDefault("Models", projectDirs.Values.First());
				case "dataaccess":
				case "data access":
					return projectDirs.GetValueOrDefault("DataAccess", projectDirs.Values.First());
				case "business":
				case "businesslogic":
				case "business logic":
					return projectDirs.GetValueOrDefault("Business", projectDirs.Values.First());
				case "web":
				case "webform":
					return projectDirs.GetValueOrDefault("Web", projectDirs.Values.First());
				default:
					return projectDirs.GetValueOrDefault("Common", projectDirs.Values.First());
			}
		}

		private string GenerateProjectFileContent(string projectName, string projectDescription)
		{
			var sb = new StringBuilder();

			sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
			sb.AppendLine();
			sb.AppendLine("  <PropertyGroup>");
			sb.AppendLine("    <TargetFramework>net6.0</TargetFramework>");
			sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
			sb.AppendLine("    <Nullable>enable</Nullable>");
			sb.AppendLine("  </PropertyGroup>");
			sb.AppendLine();

			if (projectName.Contains("Web"))
			{
				sb.AppendLine("  <PropertyGroup>");
				sb.AppendLine("    <OutputType>Library</OutputType>");
				sb.AppendLine("  </PropertyGroup>");
				sb.AppendLine();
				sb.AppendLine("  <ItemGroup>");
				sb.AppendLine("    <PackageReference Include=\"Microsoft.AspNet.WebPages\" Version=\"3.2.9\" />");
				sb.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" Version=\"6.0.0\" />");
				sb.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"6.0.0\" />");
				sb.AppendLine("  </ItemGroup>");
			}
			else
			{
				sb.AppendLine("  <ItemGroup>");
				sb.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" Version=\"6.0.0\" />");

				if (projectName.Contains("DataAccess"))
				{
					sb.AppendLine("    <PackageReference Include=\"Npgsql\" Version=\"6.0.0\" />");
					sb.AppendLine("    <PackageReference Include=\"Dapper\" Version=\"2.0.0\" />");
				}

				sb.AppendLine("  </ItemGroup>");
			}

			sb.AppendLine();
			sb.AppendLine("</Project>");

			return sb.ToString();
		}

		private string GenerateSolutionFileContent(List<string> projects)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
			sb.AppendLine("# Visual Studio Version 17");
			sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
			sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
			sb.AppendLine();

			foreach (var project in projects)
			{
				var projectName = project.Split('-')[0].Trim();
				var projectGuid = Guid.NewGuid().ToString().ToUpper();

				sb.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{projectName}\", \"{projectName}\\{projectName}.csproj\", \"{{{projectGuid}}}\"");
				sb.AppendLine("EndProject");
			}

			sb.AppendLine("Global");
			sb.AppendLine("	GlobalSection(SolutionConfigurationPlatforms) = preSolution");
			sb.AppendLine("		Debug|Any CPU = Debug|Any CPU");
			sb.AppendLine("		Release|Any CPU = Release|Any CPU");
			sb.AppendLine("	EndGlobalSection");
			sb.AppendLine("EndGlobal");

			return sb.ToString();
		}

		private int CountGeneratedFiles(string directory)
		{
			return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length;
		}

		private List<string> GetGeneratedFilesList(string directory)
		{
			return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
				.Select(f => Path.GetRelativePath(directory, f))
				.ToList();
		}

		private string ToCamelCase(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;

			return char.ToLowerInvariant(input[0]) + input.Substring(1);
		}
	}

	public class CodeFile
	{
		public string FileName { get; set; }
		public string Type { get; set; }
		public string Content { get; set; }
	}
}
