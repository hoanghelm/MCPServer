using System;
using System.Collections.Generic;

namespace CSharpLegacyMigrationMCP.Models
{
	public class FileMigrationResult
	{
		public bool Success { get; set; }
		public ProjectFile CurrentFile { get; set; }
		public List<string> DalFilesCreated { get; set; } = new List<string>();
		public List<string> BalFilesCreated { get; set; } = new List<string>();
		public List<string> InterfacesCreated { get; set; } = new List<string>();
		public string Error { get; set; }
		public int FilesMigrated { get; set; }
		public int FilesRemaining { get; set; }
		public double ProgressPercentage { get; set; }
		public bool HasMoreFiles { get; set; }
		public int TotalMigrated { get; set; }
		public int TotalFailed { get; set; }

		public string MigrationPrompt { get; set; }
		public bool RequiresAiProcessing { get; set; }
	}

	public interface IFileMigrator
	{
		Task<FileMigrationResult> MigrateNextFileAsync(string projectId);
		Task<FileMigrationResult> MigrateFileAsync(ProjectFile file, MigrationProject project);
		Task<FileMigrationResult> ProcessAiResponseAsync(ProjectFile file, MigrationProject project, string aiResponse);
	}

	public interface IMigrationPromptBuilder
	{
		Task<string> BuildMigrationPromptAsync(ProjectFile file, string projectName, string projectId, MigrationProject project);
		string BuildMigrationPrompt(ProjectFile file, string projectName);
		Dictionary<string, string> ParseMigratedCode(string aiResponse);
	}

	public class ProjectStructureInfo
	{
		public string DalProjectPath { get; set; }
		public string BalProjectPath { get; set; }
		public List<string> ExistingDalFiles { get; set; } = new List<string>();
		public List<string> ExistingBalFiles { get; set; } = new List<string>();
		public List<string> ExistingInterfaces { get; set; } = new List<string>();
		public List<string> ExistingModels { get; set; } = new List<string>();
	}

	public class ProjectFile
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string ProjectId { get; set; }
		public string FilePath { get; set; }
		public string FileName { get; set; }
		public string FileType { get; set; } // WebForm, DataAccess, BusinessLogic, Model, Utility
		public string SourceCode { get; set; }
		public MigrationStatus MigrationStatus { get; set; } = MigrationStatus.Pending;
		public DateTime? MigratedAt { get; set; }
		public string DalOutputPath { get; set; } // Semicolon-separated list of created DAL files
		public string BalOutputPath { get; set; } // Semicolon-separated list of created BAL files
		public string ErrorMessage { get; set; }
		public int Complexity { get; set; } // 1-5 scale
		public List<string> Classes { get; set; } = new List<string>();
		public List<string> Dependencies { get; set; } = new List<string>();

		public List<string> CreatedDalFiles { get; set; } = new List<string>();
		public List<string> CreatedBalFiles { get; set; } = new List<string>();
		public DateTime? LastPromptGenerated { get; set; }
		public string MigrationNotes { get; set; }

		public Dictionary<string, string> MigratedCode { get; set; } = new Dictionary<string, string>();
	}

	public class WorkspaceAnalysis
	{
		public string ProjectId { get; set; } = Guid.NewGuid().ToString();
		public string ProjectName { get; set; }
		public string WorkspacePath { get; set; }
		public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
		public int TotalFiles { get; set; }
		public List<ProjectFile> CsFiles { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> AspxFiles { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> AscxFiles { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> WebForms { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> DataAccessFiles { get; set; } = new List<ProjectFile>();
		public List<ProjectFile> BusinessLogicFiles { get; set; } = new List<ProjectFile>();
		public Dictionary<string, int> FilesByType { get; set; } = new Dictionary<string, int>();
		public Dictionary<string, int> FilesByComplexity { get; set; } = new Dictionary<string, int>();
		public ProjectArchitecture ProjectArchitecture { get; set; }
	}

	public class MigrationProject
	{
		public string ProjectId { get; set; }
		public string ProjectName { get; set; }
		public string WorkspacePath { get; set; }
		public string DalProjectPath { get; set; }
		public string BalProjectPath { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public ProjectStatus Status { get; set; } = ProjectStatus.Initialized;
	}

	public class MigrationProgress
	{
		public string ProjectId { get; set; }
		public string CurrentStatus { get; set; }
		public int TotalFiles { get; set; }
		public int MigratedFiles { get; set; }
		public int FailedFiles { get; set; }
		public int PendingFiles { get; set; }
		public double ProgressPercentage { get; set; }
		public string DalProjectPath { get; set; }
		public string BalProjectPath { get; set; }
		public DateTime? StartedAt { get; set; }
		public DateTime? LastUpdated { get; set; }
		public DateTime? EstimatedCompletion { get; set; }
	}

	public class MigrationStartResult
	{
		public bool Success { get; set; }
		public bool ProjectsCreated { get; set; }
		public string DalProjectPath { get; set; }
		public string BalProjectPath { get; set; }
		public int TotalFilesToMigrate { get; set; }
		public string Error { get; set; }
	}

	public class RetryResult
	{
		public int FailedFilesCount { get; set; }
		public bool RetryStarted { get; set; }
	}

	// Enums
	public enum MigrationStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
		Skipped
	}

	public enum ProjectStatus
	{
		Initialized,
		Analyzing,
		ReadyForMigration,
		Migrating,
		Completed,
		Failed
	}

	public enum FileType
	{
		WebForm,
		CodeBehind,
		UserControl,
		DataAccess,
		BusinessLogic,
		Model,
		Utility,
		Unknown
	}

	// Interfaces
	public interface IWorkspaceAnalyzer
	{
		Task<WorkspaceAnalysis> AnalyzeWorkspaceAsync(string workspacePath);
	}

	public interface IProjectCreator
	{
		Task<(string dalPath, string balPath)> CreateProjectsAsync(string workspacePath, string projectName);
	}

	public interface IMigrationOrchestrator
	{
		Task<MigrationStartResult> StartMigrationAsync(string projectId, bool createProjects);
		Task<RetryResult> RetryFailedFilesAsync(string projectId);
	}

	public interface IDataRepository
	{
		// Project operations
		Task<string> SaveWorkspaceAnalysisAsync(WorkspaceAnalysis analysis);
		Task<WorkspaceAnalysis> GetWorkspaceAnalysisAsync(string projectId);
		Task<MigrationProject> GetMigrationProjectAsync(string projectId);
		Task SaveMigrationProjectAsync(MigrationProject project);

		// File operations
		Task<List<ProjectFile>> GetProjectFilesAsync(string projectId, string statusFilter = "all");
		Task<ProjectFile> GetNextUnmigratedFileAsync(string projectId);
		Task UpdateFileStatusAsync(string fileId, MigrationStatus status, string error = null);
		Task SaveMigratedFileAsync(ProjectFile file);

		// Status operations
		Task<MigrationProgress> GetMigrationStatusAsync(string projectId);
		Task UpdateMigrationProgressAsync(string projectId);

		// Database operations
		Task<bool> TestConnectionAsync();
		Task MigrateAsync();
	}

	// Exceptions
	public class MigrationException : Exception
	{
		public MigrationException(string message) : base(message) { }
		public MigrationException(string message, Exception innerException) : base(message, innerException) { }
	}
}