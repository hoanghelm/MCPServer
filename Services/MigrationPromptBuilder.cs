using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpLegacyMigrationMCP.Models;
using Microsoft.Extensions.Logging;

namespace CSharpLegacyMigrationMCP.Services
{
	public class MigrationPromptBuilder : IMigrationPromptBuilder
	{
		private readonly IDependencyAnalyzer _dependencyAnalyzer;
		private readonly ILogger<MigrationPromptBuilder> _logger;

		public MigrationPromptBuilder(IDependencyAnalyzer dependencyAnalyzer, ILogger<MigrationPromptBuilder> logger)
		{
			_dependencyAnalyzer = dependencyAnalyzer;
			_logger = logger;
		}

		public async Task<string> BuildMigrationPromptAsync(ProjectFile file, string projectName, string projectId, MigrationProject project)
		{
			var prompt = new StringBuilder();

			prompt.AppendLine("You are a C# migration expert with DIRECT FILE ACCESS. You will migrate legacy .NET WebForms code to a modern layered architecture and WRITE FILES DIRECTLY to the target projects.");
			prompt.AppendLine();
			prompt.AppendLine("CRITICAL: You have DIRECT ACCESS to write files. Generate COMPLETE, PRODUCTION-READY code and write it directly to the specified paths. Do not ask for permission - just create the files.");
			prompt.AppendLine();
			prompt.AppendLine("IMPORTANT: This project uses PostgreSQL database. All database operations must use PostgreSQL/Npgsql, NOT SQL Server.");
			prompt.AppendLine();
			prompt.AppendLine("PROJECT CONTEXT:");
			prompt.AppendLine($"- Original Project: {projectName}");
			prompt.AppendLine($"- File Type: {file.FileType}");
			prompt.AppendLine($"- File Name: {file.FileName}");
			prompt.AppendLine($"- Source Path: {file.FilePath}");
			prompt.AppendLine($"- DAL Project: {projectName}.DAL");
			prompt.AppendLine($"- BAL Project: {projectName}.BAL");
			prompt.AppendLine($"- Database: PostgreSQL (using Npgsql)");
			prompt.AppendLine();

			// Get project paths and existing structure
			var projectInfo = GetProjectStructureInfo(project);
			if (projectInfo != null)
			{
				prompt.AppendLine("TARGET PROJECT STRUCTURE:");
				prompt.AppendLine($"- DAL Project Path: {projectInfo.DalProjectPath}");
				prompt.AppendLine($"- BAL Project Path: {projectInfo.BalProjectPath}");
				prompt.AppendLine();

				// Include existing files for context
				if (projectInfo.ExistingDalFiles.Any())
				{
					prompt.AppendLine("EXISTING DAL FILES (for reference and relationships):");
					foreach (var existingFile in projectInfo.ExistingDalFiles.Take(10)) // Limit to avoid bloat
					{
						prompt.AppendLine($"- {existingFile}");
					}
					if (projectInfo.ExistingDalFiles.Count > 10)
					{
						prompt.AppendLine($"... and {projectInfo.ExistingDalFiles.Count - 10} more files");
					}
					prompt.AppendLine();
				}

				if (projectInfo.ExistingBalFiles.Any())
				{
					prompt.AppendLine("EXISTING BAL FILES (for reference and relationships):");
					foreach (var existingFile in projectInfo.ExistingBalFiles.Take(10)) // Limit to avoid bloat
					{
						prompt.AppendLine($"- {existingFile}");
					}
					if (projectInfo.ExistingBalFiles.Count > 10)
					{
						prompt.AppendLine($"... and {projectInfo.ExistingBalFiles.Count - 10} more files");
					}
					prompt.AppendLine();
				}
			}

			// Get related migrated files for better context
			var relatedFileContents = await _dependencyAnalyzer.GetRelatedMigratedFileContentsAsync(file, projectId, project);
			if (relatedFileContents.Any())
			{
				prompt.AppendLine("🔗 RELATED MIGRATED FILES (for reference and consistency):");
				prompt.AppendLine("CRITICAL: Use these files as examples for naming conventions, patterns, and structure.");
				prompt.AppendLine("Follow the EXACT same patterns shown below for maximum consistency:");
				prompt.AppendLine();

				foreach (var relatedContent in relatedFileContents)
				{
					prompt.AppendLine(relatedContent);
					prompt.AppendLine();
				}

				prompt.AppendLine("🎯 CONSISTENCY REQUIREMENTS:");
				prompt.AppendLine("- Follow the EXACT naming conventions shown in the related files above");
				prompt.AppendLine("- Use the SAME namespace structure and organization");
				prompt.AppendLine("- Match the constructor dependency injection patterns");
				prompt.AppendLine("- Use the SAME error handling and logging patterns");
				prompt.AppendLine("- Follow the SAME async/await patterns and method signatures");
				prompt.AppendLine("- Use the SAME PostgreSQL connection and command patterns");
				prompt.AppendLine("- Maintain the SAME code style and formatting");
				prompt.AppendLine();
			}
			else
			{
				prompt.AppendLine("ℹ️  No related migrated files found. This appears to be one of the first files being migrated.");
				prompt.AppendLine("Create clean, modern patterns that other files can follow.");
				prompt.AppendLine();
			}

			prompt.AppendLine("SEPARATE DAL AND BAL PROJECT STRATEGY:");
			prompt.AppendLine("This migration creates TWO SEPARATE projects:");
			prompt.AppendLine($"1. **{projectName}.DAL** - Data Access Layer Project");
			prompt.AppendLine("   - Contains: DataAccess/, Models/, Interfaces/ folders");
			prompt.AppendLine("   - Handles: Database operations, entity models, repository interfaces and implementations");
			prompt.AppendLine($"2. **{projectName}.BAL** - Business Access Layer Project");
			prompt.AppendLine("   - Contains: BusinessLogics/, Utils/, Interfaces/ folders");
			prompt.AppendLine("   - Handles: Business logic, services, DTOs, validation, utilities");
			prompt.AppendLine("   - References: DAL project for data access");
			prompt.AppendLine();

			prompt.AppendLine("DIRECT FILE WRITING INSTRUCTIONS:");
			prompt.AppendLine("1. **WRITE FILES TO SEPARATE PROJECTS**: Write DAL files to DAL project, BAL files to BAL project");
			prompt.AppendLine("2. **CREATE APPROPRIATE FOLDER STRUCTURE**: Use DataAccess/, BusinessLogics/, Interfaces/, Models/, Utils/");
			prompt.AppendLine("3. **MAINTAIN PROJECT SEPARATION**: DAL handles data, BAL handles business logic");
			prompt.AppendLine("4. **REFERENCE EXISTING FILES**: Check existing files for consistent naming and dependencies");
			prompt.AppendLine("5. **UPDATE RELATED FILES**: If needed, update existing interfaces or add new dependencies");
			prompt.AppendLine();

			prompt.AppendLine("MIGRATION REQUIREMENTS - WRITE COMPLETE FILES:");
			prompt.AppendLine("1. **COMPLETE INTERFACES**: Write full interface files with ALL methods, XML documentation, and async variants");
			prompt.AppendLine("2. **FULL IMPLEMENTATIONS**: Write complete implementation files with every method fully implemented");
			prompt.AppendLine("3. **COMPREHENSIVE ERROR HANDLING**: Include try-catch blocks, logging, and proper exception management");
			prompt.AppendLine("4. **COMPLETE DATABASE OPERATIONS**: Include full SQL queries, parameter handling, and result mapping");
			prompt.AppendLine("5. **DEPENDENCY INJECTION**: Complete constructor injection with proper validation");
			prompt.AppendLine("6. **ASYNC/AWAIT PATTERNS**: All database operations must be async with proper cancellation token support");
			prompt.AppendLine("7. **SOLID PRINCIPLES**: Follow all SOLID principles with proper abstractions");
			prompt.AppendLine("8. **C# FEATURES**: Use C# features compatible with netcoreapp2.0");
			prompt.AppendLine("9. **POSTGRESQL SPECIFIC**: Use Npgsql 3.2.7 with proper PostgreSQL syntax and features");
			prompt.AppendLine("10. **PRODUCTION READY**: Include logging, validation, configuration, and proper resource disposal");
			prompt.AppendLine();

			prompt.AppendLine("POSTGRESQL INTEGRATION REQUIREMENTS (Npgsql 3.2.7):");
			prompt.AppendLine("- Use NpgsqlConnection for all database connections");
			prompt.AppendLine("- Use NpgsqlCommand with proper parameter binding");
			prompt.AppendLine("- Include connection string configuration (IConfiguration)");
			prompt.AppendLine("- Use NpgsqlTransaction for multi-statement operations");
			prompt.AppendLine("- Implement proper connection pooling and disposal");
			prompt.AppendLine("- Use PostgreSQL-specific SQL syntax (RETURNING, LIMIT, etc.)");
			prompt.AppendLine("- Include proper type mapping for PostgreSQL data types");
			prompt.AppendLine("- Compatible with netcoreapp2.0 framework");
			prompt.AppendLine();

			// Add specific instructions based on file type
			AddFileTypeSpecificInstructions(prompt, file);

			// Add comprehensive SQL Server to PostgreSQL conversion rules
			if (CSharpLegacyMigrationMCP.Helpers.PostgreSqlHelper.UsesSqlServer(file.SourceCode))
			{
				AddSqlServerToPostgreSqlConversionRules(prompt);
			}

			prompt.AppendLine("FILE WRITING STRATEGY:");
			prompt.AppendLine("For each file you create, follow this pattern:");
			prompt.AppendLine("1. Determine the appropriate folder structure (Interfaces/, Repositories/, Services/, Models/)");
			prompt.AppendLine("2. Generate the complete file content with all necessary using statements");
			prompt.AppendLine("3. Write the file directly to the target path");
			prompt.AppendLine("4. Ensure consistency with existing project patterns");
			prompt.AppendLine("5. Update any related files if necessary");
			prompt.AppendLine();

			prompt.AppendLine("FILE ORGANIZATION BY PROJECT:");
			if (projectInfo != null)
			{
				prompt.AppendLine($"**DAL PROJECT ({projectName}.DAL):**");
				prompt.AppendLine($"- Data Interfaces: {projectInfo.DalProjectPath}/Interfaces/I{{EntityName}}Interfaces.cs");
				prompt.AppendLine($"- Data Implementations: {projectInfo.DalProjectPath}/DataAccess/{{EntityName}}DAL.cs");
				prompt.AppendLine($"- Entity Models: {projectInfo.DalProjectPath}/Models/{{EntityName}}.cs");
				prompt.AppendLine();
				prompt.AppendLine($"**BAL PROJECT ({projectName}.BAL):**");
				prompt.AppendLine($"- Business Interfaces: {projectInfo.BalProjectPath}/Interfaces/I{{EntityName}}Interfaces.cs");
				prompt.AppendLine($"- Business Logic: {projectInfo.BalProjectPath}/BusinessLogics/{{EntityName}}BAL.cs");
				prompt.AppendLine($"- Utilities: {projectInfo.BalProjectPath}/Utils/{{UtilityName}}.cs");
			}
			prompt.AppendLine();

			prompt.AppendLine("MANDATORY IMPLEMENTATION CHECKLIST:");
			prompt.AppendLine("✓ All using statements included");
			prompt.AppendLine("✓ Complete namespace declarations");
			prompt.AppendLine("✓ Full class implementations (no partial implementations)");
			prompt.AppendLine("✓ All method bodies implemented (no empty methods)");
			prompt.AppendLine("✓ Proper constructor dependency injection");
			prompt.AppendLine("✓ Complete error handling with try-catch");
			prompt.AppendLine("✓ Logging implementation (ILogger)");
			prompt.AppendLine("✓ Configuration handling (IConfiguration)");
			prompt.AppendLine("✓ Async/await patterns implemented");
			prompt.AppendLine("✓ PostgreSQL connection and command implementation");
			prompt.AppendLine("✓ Proper resource disposal (using statements)");
			prompt.AppendLine("✓ Input validation and business rules");
			prompt.AppendLine("✓ Complete SQL queries with parameter binding");
			prompt.AppendLine("✓ Proper exception types and messages");
			prompt.AppendLine("✓ XML documentation for public members");
			prompt.AppendLine("✓ Files written directly to target paths");
			prompt.AppendLine();

			prompt.AppendLine("SOURCE CODE TO MIGRATE:");
			prompt.AppendLine("```csharp");
			prompt.AppendLine(file.SourceCode);
			prompt.AppendLine("```");
			prompt.AppendLine();

			prompt.AppendLine("FINAL INSTRUCTION: You have DIRECT FILE ACCESS. Write COMPLETE, PRODUCTION-READY files directly to the target project paths. Do not provide code in your response - instead, CREATE THE ACTUAL FILES. After creating files, provide a summary of what was created and where.");

			return prompt.ToString();
		}

		private ProjectStructureInfo GetProjectStructureInfo(MigrationProject project)
		{
			if (project == null) return null;

			return new ProjectStructureInfo
			{
				DalProjectPath = project.DalProjectPath,
				BalProjectPath = project.BalProjectPath,
				ExistingDalFiles = GetExistingFiles(project.DalProjectPath),
				ExistingBalFiles = GetExistingFiles(project.BalProjectPath)
			};
		}

		private List<string> GetExistingFiles(string projectPath)
		{
			var files = new List<string>();

			if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
				return files;

			try
			{
				var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
					.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
							   !f.Contains("/bin/") && !f.Contains("/obj/"))
					.Select(f => Path.GetRelativePath(projectPath, f))
					.ToList();

				files.AddRange(csFiles);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, $"Could not scan existing files in {projectPath}");
			}

			return files;
		}

		private void AddFileTypeSpecificInstructions(StringBuilder prompt, ProjectFile file)
		{
			switch (file.FileType)
			{
				case "WebForm":
				case "CodeBehind":
					prompt.AppendLine("WEBFORM COMPLETE MIGRATION TO SEPARATE DAL/BAL:");
					prompt.AppendLine("**DAL PROJECT FILES TO CREATE:**");
					prompt.AppendLine("- Data interfaces in DAL/Interfaces/I{Entity}Interfaces.cs");
					prompt.AppendLine("- Data access implementations in DAL/DataAccess/{Entity}DAL.cs");
					prompt.AppendLine("- Entity models in DAL/Models/{Entity}.cs");
					prompt.AppendLine("**BAL PROJECT FILES TO CREATE:**");
					prompt.AppendLine("- Business interfaces in BAL/Interfaces/I{Entity}Interfaces.cs");
					prompt.AppendLine("- Business logic in BAL/BusinessLogics/{Entity}BAL.cs");
					prompt.AppendLine("- Utilities in BAL/Utils/ folder if needed");
					prompt.AppendLine("**EXTRACTION STRATEGY:**");
					prompt.AppendLine("- Extract ALL database operations from Page_Load and event handlers → DAL");
					prompt.AppendLine("- Extract ALL business logic and validation → BAL");
					prompt.AppendLine("- Keep only presentation logic in original file");
					prompt.AppendLine();
					break;

				case "DataAccess":
					prompt.AppendLine("DATA ACCESS MIGRATION TO DAL PROJECT:");
					prompt.AppendLine("**DAL PROJECT STRUCTURE:**");
					prompt.AppendLine("- Interfaces: DAL/Interfaces/I{Entity}Interfaces.cs");
					prompt.AppendLine("- Implementations: DAL/DataAccess/{Entity}DAL.cs");
					prompt.AppendLine("- Models: DAL/Models/{Entity}.cs");
					prompt.AppendLine("**IMPLEMENTATION REQUIREMENTS:**");
					prompt.AppendLine("- Use NpgsqlConnection with complete connection handling");
					prompt.AppendLine("- Direct Npgsql implementation (NO Dapper)");
					prompt.AppendLine("- Include complete transaction handling");
					prompt.AppendLine("- All methods must be async with proper error handling");
					prompt.AppendLine();
					break;

				case "BusinessLogic":
					prompt.AppendLine("BUSINESS LOGIC MIGRATION TO BAL PROJECT:");
					prompt.AppendLine("**BAL PROJECT STRUCTURE:**");
					prompt.AppendLine("- Interfaces: BAL/Interfaces/I{Entity}Interfaces.cs");
					prompt.AppendLine("- Implementations: BAL/BusinessLogics/{Entity}BAL.cs");
					prompt.AppendLine("- Utilities: BAL/Utils/{Utility}.cs");
					prompt.AppendLine("**IMPLEMENTATION REQUIREMENTS:**");
					prompt.AppendLine("- Create business interfaces with ALL business methods");
					prompt.AppendLine("- Write complete business logic implementations");
					prompt.AppendLine("- Include comprehensive validation and business rules");
					prompt.AppendLine("- Reference DAL project for data access");
					prompt.AppendLine("- Implement proper exception handling and logging");
					prompt.AppendLine();
					break;
			}
		}

		private void AddSqlServerToPostgreSqlConversionRules(StringBuilder prompt)
		{
			prompt.AppendLine("SQL SERVER TO POSTGRESQL COMPLETE CONVERSION RULES:");
			prompt.AppendLine("**CONNECTION AND COMMANDS:**");
			prompt.AppendLine("- Replace SqlConnection with NpgsqlConnection");
			prompt.AppendLine("- Replace SqlCommand with NpgsqlCommand");
			prompt.AppendLine("- Replace SqlDataAdapter with NpgsqlDataAdapter");
			prompt.AppendLine("- Replace SqlDataReader with NpgsqlDataReader");
			prompt.AppendLine("- Replace SqlTransaction with NpgsqlTransaction");
			prompt.AppendLine("- Replace SqlParameter with NpgsqlParameter");
			prompt.AppendLine();
			prompt.AppendLine("**SQL SYNTAX CONVERSIONS:**");
			prompt.AppendLine("- TOP N → LIMIT N");
			prompt.AppendLine("- GETDATE() → CURRENT_TIMESTAMP or NOW()");
			prompt.AppendLine("- ISNULL(col, default) → COALESCE(col, default)");
			prompt.AppendLine("- [brackets] → \"quotes\" for identifiers");
			prompt.AppendLine("- IDENTITY → SERIAL or BIGSERIAL for auto-increment");
			prompt.AppendLine("- @@IDENTITY → RETURNING id clause");
			prompt.AppendLine("- LEN() → LENGTH()");
			prompt.AppendLine("- CHARINDEX() → POSITION()");
			prompt.AppendLine("- SUBSTRING() → SUBSTR()");
			prompt.AppendLine("- DATEADD() → interval arithmetic");
			prompt.AppendLine("- DATEDIFF() → date/time subtraction");
			prompt.AppendLine();
			prompt.AppendLine("**DATA TYPE CONVERSIONS:**");
			prompt.AppendLine("- NVARCHAR → VARCHAR or TEXT");
			prompt.AppendLine("- DATETIME → TIMESTAMP");
			prompt.AppendLine("- BIT → BOOLEAN");
			prompt.AppendLine("- UNIQUEIDENTIFIER → UUID");
			prompt.AppendLine("- VARBINARY → BYTEA");
			prompt.AppendLine();
		}

		public string BuildMigrationPrompt(ProjectFile file, string projectName)
		{
			// Legacy method for backward compatibility
			throw new NotImplementedException("Use BuildMigrationPromptAsync instead");
		}

		public Dictionary<string, string> ParseMigratedCode(string aiResponse)
		{
			// This method is no longer needed since AI writes files directly
			// But keeping for backward compatibility
			return new Dictionary<string, string>();
		}
	}

	public class ProjectStructureInfo
	{
		public string DalProjectPath { get; set; }
		public string BalProjectPath { get; set; }
		public List<string> ExistingDalFiles { get; set; } = new List<string>();
		public List<string> ExistingBalFiles { get; set; } = new List<string>();
	}
}