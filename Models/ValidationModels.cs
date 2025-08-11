using System;
using System.Collections.Generic;

namespace CSharpLegacyMigrationMCP.Models
{
	public class MigrationValidationResult
	{
		public bool IsValid { get; set; } = true;
		public string ProjectId { get; set; } = string.Empty;
		public List<string> Issues { get; set; } = new List<string>();
		public List<string> Warnings { get; set; } = new List<string>();
		public List<string> GoodDependencies { get; set; } = new List<string>();
		
		public OriginalProjectState OriginalProjectState { get; set; }
		public MigratedProjectState MigratedProjectState { get; set; }
		public DatabaseValidation DatabaseValidation { get; set; }
		
		public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
		public TimeSpan ValidationDuration { get; set; }
	}

	public class OriginalProjectState
	{
		public int TotalCsFiles { get; set; }
		public int TotalAspxFiles { get; set; }
		public bool HasDataSets { get; set; }
		public bool HasExistingDAL { get; set; }
		public bool HasExistingBAL { get; set; }
		public ProjectArchitecture Architecture { get; set; }
	}

	public class MigratedProjectState
	{
		public int TotalMigratedFiles { get; set; }
		public int DalFilesCount { get; set; }
		public int BalFilesCount { get; set; }
		public List<string> MissingFiles { get; set; } = new List<string>();
		public List<string> OrphanedFiles { get; set; } = new List<string>();
	}

	public class DatabaseValidation
	{
		public bool IsConnectable { get; set; }
		public bool HasRequiredSchema { get; set; }
		public List<string> Issues { get; set; } = new List<string>();
		public string ConnectionString { get; set; } = string.Empty;
	}

	public class FileValidationInfo
	{
		public string FilePath { get; set; } = string.Empty;
		public bool Exists { get; set; }
		public bool IsModifiedAfterMigration { get; set; }
		public bool HasSyntaxErrors { get; set; }
		public List<string> Issues { get; set; } = new List<string>();
	}

	public class DependencyValidationInfo
	{
		public string FileId { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public List<string> Dependencies { get; set; } = new List<string>();
		public List<string> MigratedDependencies { get; set; } = new List<string>();
		public List<string> UnmigratedDependencies { get; set; } = new List<string>();
		public bool HasCircularDependencies { get; set; }
	}

	public enum ValidationSeverity
	{
		Info,
		Warning,
		Error,
		Critical
	}

	public class ValidationIssue
	{
		public ValidationSeverity Severity { get; set; }
		public string Category { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public string Recommendation { get; set; } = string.Empty;
		public bool IsAutoFixable { get; set; }
	}
}