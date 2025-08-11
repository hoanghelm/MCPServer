using System;
using System.IO;
using System.Threading.Tasks;
using CSharpLegacyMigrationMCP.Models;
using Microsoft.Extensions.Logging;

namespace CSharpLegacyMigrationMCP.Services
{
	public class ProjectCreator : IProjectCreator
	{
		private readonly ILogger<ProjectCreator> _logger;

		public ProjectCreator(ILogger<ProjectCreator> logger)
		{
			_logger = logger;
		}

		public async Task<(string dalPath, string balPath)> CreateProjectsAsync(string workspacePath, string projectName)
		{
			try
			{
				_logger.LogInformation($"Creating DAL and BAL projects for {projectName}");

				var dalProjectName = $"{projectName}.DAL";
				var balProjectName = $"{projectName}.BAL";

				var dalPath = Path.Combine(workspacePath, dalProjectName);
				var balPath = Path.Combine(workspacePath, balProjectName);

				// Create project directories
				Directory.CreateDirectory(dalPath);
				Directory.CreateDirectory(balPath);

				// Create folder structure for DAL
				CreateProjectStructure(dalPath, dalProjectName, "DataAccess");

				// Create folder structure for BAL  
				CreateProjectStructure(balPath, balProjectName, "Business");

				// Create .csproj files
				await CreateProjectFileAsync(dalPath, dalProjectName, "DataAccess");
				await CreateProjectFileAsync(balPath, balProjectName, "Business");

				// Add reference from BAL to DAL
				await AddProjectReferenceAsync(balPath, balProjectName, dalPath, dalProjectName);

				_logger.LogInformation($"Successfully created projects at {dalPath} and {balPath}");

				return (dalPath, balPath);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating projects");
				throw new MigrationException($"Failed to create projects: {ex.Message}", ex);
			}
		}

		private void CreateProjectStructure(string projectPath, string projectName, string projectType)
		{
			var folders = new[]
			{
				"Interfaces",
			};

			// Add specific folders based on project type
			if (projectType == "DataAccess")
			{
				Directory.CreateDirectory(Path.Combine(projectPath, "DataAccess"));
				Directory.CreateDirectory(Path.Combine(projectPath, "Models"));
			}
			else if (projectType == "Business")
			{
				Directory.CreateDirectory(Path.Combine(projectPath, "BusinessLogics"));
				Directory.CreateDirectory(Path.Combine(projectPath, "Utils"));
			}
		}

		private async Task CreateProjectFileAsync(string projectPath, string projectName, string projectType)
		{
			var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>netcoreapp2.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Autofac.Extensions.DependencyInjection"" Version=""4.0.0"" />
    <PackageReference Include=""Autofac.Web"" Version=""4.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.Configuration"" Version=""5.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""5.0.0"" />
    <PackageReference Include=""Npgsql"" Version=""3.2.7"" />
    <PackageReference Include=""System.Security.Cryptography.Cng"" Version=""5.0.0"" />
  </ItemGroup>

</Project>";

			var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
			await File.WriteAllTextAsync(csprojPath, csprojContent);
		}

		private async Task AddProjectReferenceAsync(string fromProjectPath, string fromProjectName,
			string toProjectPath, string toProjectName)
		{
			var csprojPath = Path.Combine(fromProjectPath, $"{fromProjectName}.csproj");
			var csprojContent = await File.ReadAllTextAsync(csprojPath);

			var relativePath = Path.GetRelativePath(fromProjectPath, Path.Combine(toProjectPath, $"{toProjectName}.csproj"));

			var referenceSection = $@"
  <ItemGroup>
    <ProjectReference Include=""{relativePath}"" />
  </ItemGroup>

</Project>";

			csprojContent = csprojContent.Replace("</Project>", referenceSection);
			await File.WriteAllTextAsync(csprojPath, csprojContent);
		}
	}
}