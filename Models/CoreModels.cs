using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CSharpLegacyMigrationMCP.Models
{
	// Core Models with consistent numeric types
	public class AnalysisResult
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string DirectoryPath { get; set; }
		public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
		public int TotalFiles { get; set; }
		public int TotalClasses { get; set; }
		public int TotalMethods { get; set; }
		public double ComplexityScore { get; set; } // Changed to double consistently
		public List<CodeStructure> CodeStructures { get; set; } = new List<CodeStructure>();
		public List<string> Dependencies { get; set; } = new List<string>();
	}

	public class CodeStructure
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string AnalysisId { get; set; }
		public string FilePath { get; set; }
		public string ClassName { get; set; }
		public string Namespace { get; set; }
		public CodeStructureType Type { get; set; }
		public List<MethodStructure> Methods { get; set; } = new List<MethodStructure>();
		public List<PropertyStructure> Properties { get; set; } = new List<PropertyStructure>();
		public List<string> Dependencies { get; set; } = new List<string>();
		public string SourceCode { get; set; }
		public bool IsMigrated { get; set; } = false;
		public DateTime? MigratedAt { get; set; }
		public string MigratedCode { get; set; }
		public double ComplexityScore { get; set; } // Changed to double consistently
		public List<string> DatabaseTables { get; set; } = new List<string>();
	}

	public class MethodStructure
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string Name { get; set; }
		public string ReturnType { get; set; }
		public List<ParameterStructure> Parameters { get; set; } = new List<ParameterStructure>();
		public int LinesOfCode { get; set; }
		public double ComplexityScore { get; set; } // Changed to double consistently
		public List<string> Dependencies { get; set; } = new List<string>();
		public string SourceCode { get; set; }
		public bool HasDatabaseAccess { get; set; }
		public List<string> DatabaseOperations { get; set; } = new List<string>();
		public bool IsMigrated { get; set; } = false;
		public string MigratedCode { get; set; }
	}

	public class ParameterStructure
	{
		public string Name { get; set; }
		public string Type { get; set; }
		public bool IsOptional { get; set; }
		public string DefaultValue { get; set; }
	}

	public class PropertyStructure
	{
		public string Name { get; set; }
		public string Type { get; set; }
		public bool HasGetter { get; set; }
		public bool HasSetter { get; set; }
		public bool IsPublic { get; set; }
	}

	public class MigrationStructure
	{
		public string AnalysisId { get; set; }
		public List<InterfaceStructure> BusinessLogicInterfaces { get; set; } = new List<InterfaceStructure>();
		public List<InterfaceStructure> DataAccessInterfaces { get; set; } = new List<InterfaceStructure>();
		public List<ModelStructure> Models { get; set; } = new List<ModelStructure>();
		public List<string> SuggestedProjects { get; set; } = new List<string>();
	}

	public class InterfaceStructure
	{
		public string Name { get; set; }
		public string Namespace { get; set; }
		public List<string> Methods { get; set; } = new List<string>();
		public string Purpose { get; set; }
		public List<string> RelatedTables { get; set; } = new List<string>();
	}

	public class ModelStructure
	{
		public string Name { get; set; }
		public string Namespace { get; set; }
		public List<PropertyStructure> Properties { get; set; } = new List<PropertyStructure>();
		public string TableName { get; set; }
	}

	public class MigrationResult
	{
		public int ChunksProcessed { get; set; }
		public int ChunksRemaining { get; set; }
		public double SuccessRate { get; set; } // Changed to double consistently
		public List<string> Errors { get; set; } = new List<string>();
	}

	public class SaveResult
	{
		public int FilesSaved { get; set; }
		public string OutputDirectory { get; set; }
		public bool WebFormGenerated { get; set; }
		public List<string> GeneratedFiles { get; set; } = new List<string>();
	}

	public class MigrationStatus
	{
		public int TotalItems { get; set; }
		public int MigratedItems { get; set; }
		public int PendingItems { get; set; }
		public int FailedItems { get; set; }
		public double ProgressPercentage { get; set; } // Changed to double consistently
		public string CurrentPhase { get; set; }
		public List<string> Errors { get; set; } = new List<string>();
	}

	public class CodeChunk
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public string AnalysisId { get; set; }
		public List<string> ClassIds { get; set; } = new List<string>();
		public string CombinedCode { get; set; }
		public int EstimatedTokens { get; set; }
		public bool IsProcessed { get; set; } = false;
		public string MigratedCode { get; set; }
		public List<string> Errors { get; set; } = new List<string>();
	}

	public enum CodeStructureType
	{
		WebForm,
		CodeBehind,
		UserControl,
		BusinessLogic,
		DataAccess,
		Utility,
		Model,
		Unknown
	}

	// Core Interfaces - unchanged
	public interface ISourceCodeAnalyzer
	{
		Task<AnalysisResult> AnalyzeDirectoryAsync(string directoryPath, bool includeSubdirectories = true);
		Task<CodeStructure> AnalyzeFileAsync(string filePath);
	}

	public interface IMigrationStructureGenerator
	{
		Task<MigrationStructure> GenerateStructureAsync(string analysisId);
		Task<List<InterfaceStructure>> GenerateDataAccessInterfacesAsync(List<CodeStructure> codeStructures);
		Task<List<InterfaceStructure>> GenerateBusinessLogicInterfacesAsync(List<CodeStructure> codeStructures);
		Task<List<ModelStructure>> GenerateModelsAsync(List<CodeStructure> codeStructures);
	}

	public interface ICodeMigrator
	{
		Task<MigrationResult> MigrateCodeChunksAsync(string analysisId, double maxTokensPercentage = 0.7);
		Task<List<CodeChunk>> CreateCodeChunksAsync(string analysisId, double maxTokensPercentage);
		Task<string> SendToClaudeAsync(string codeChunk, string context);
	}

	public interface IMigratedCodeSaver
	{
		Task<SaveResult> SaveMigratedCodeAsync(string analysisId, string outputDirectory);
		Task<bool> GenerateSampleWebFormAsync(string outputDirectory, MigrationStructure structure);
	}

	public interface IMigrationStatusService
	{
		Task<MigrationStatus> GetStatusAsync(string analysisId);
		Task UpdateStatusAsync(string analysisId, string phase, int processed, int total);
	}

	public interface IDataRepository
	{
		Task<string> SaveAnalysisResultAsync(AnalysisResult result);
		Task<AnalysisResult> GetAnalysisResultAsync(string analysisId);
		Task<List<CodeStructure>> GetCodeStructuresAsync(string analysisId);
		Task UpdateCodeStructureAsync(CodeStructure structure);
		Task SaveMigrationStructureAsync(MigrationStructure structure);
		Task<MigrationStructure> GetMigrationStructureAsync(string analysisId);
		Task SaveCodeChunkAsync(CodeChunk chunk);
		Task<List<CodeChunk>> GetCodeChunksAsync(string analysisId);
		Task UpdateCodeChunkAsync(CodeChunk chunk);
	}

	// Exceptions
	public class MigrationException : Exception
	{
		public MigrationException(string message) : base(message) { }
		public MigrationException(string message, Exception innerException) : base(message, innerException) { }
	}

	public class AnalysisException : Exception
	{
		public AnalysisException(string message) : base(message) { }
		public AnalysisException(string message, Exception innerException) : base(message, innerException) { }
	}
}