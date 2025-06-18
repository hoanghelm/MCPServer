using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpLegacyMigrationMCP.Models;
using Microsoft.Extensions.Logging;

namespace CSharpLegacyMigrationMCP.Services
{
	public class ClaudeDesktopMigrator : ICodeMigrator
	{
		private readonly ILogger<ClaudeDesktopMigrator> _logger;
		private readonly IDataRepository _repository;
		private readonly IMigrationStatusService _statusService;

		private const int AVERAGE_TOKENS_PER_CHARACTER = 4;
		private const int CONTEXT_TOKENS_RESERVED = 5000;
		private const int MAX_TOKENS_PER_CHUNK = 150000;

		public ClaudeDesktopMigrator(
			ILogger<ClaudeDesktopMigrator> logger,
			IDataRepository repository,
			IMigrationStatusService statusService)
		{
			_logger = logger;
			_repository = repository;
			_statusService = statusService;
		}

		public async Task<MigrationResult> MigrateCodeChunksAsync(string analysisId, double maxTokensPercentage = 0.7)
		{
			try
			{
				_logger.LogInformation($"Preparing code chunks for migration via Claude Desktop for analysis: {analysisId}");

				var result = new MigrationResult();

				var codeChunks = await GetOrCreateCodeChunksAsync(analysisId, maxTokensPercentage);
				var totalChunks = codeChunks.Count;
				var processedChunks = codeChunks.Count(c => c.IsProcessed);

				_logger.LogInformation($"Found {totalChunks} code chunks, {processedChunks} already processed");

				await _statusService.UpdateStatusAsync(analysisId, "Preparing for Migration", processedChunks, totalChunks);

				var unprocessedChunks = codeChunks.Where(c => !c.IsProcessed).ToList();

				if (unprocessedChunks.Any())
				{
					_logger.LogInformation($"Code chunks prepared for Claude Desktop migration. Please use the 'process_migration_chunk' tool to migrate each chunk.");

					result.ChunksProcessed = processedChunks;
					result.ChunksRemaining = unprocessedChunks.Count;
					result.SuccessRate = totalChunks > 0 ? (double)processedChunks / totalChunks : 0.0;
				}
				else
				{
					_logger.LogInformation("All code chunks have been processed");
					result.ChunksProcessed = totalChunks;
					result.ChunksRemaining = 0;
					result.SuccessRate = 1.0;
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error preparing code chunks for migration for analysis {analysisId}");
				throw new MigrationException($"Failed to prepare code chunks for migration: {ex.Message}", ex);
			}
		}

		public async Task<List<CodeChunk>> CreateCodeChunksAsync(string analysisId, double maxTokensPercentage)
		{
			try
			{
				var codeStructures = await _repository.GetCodeStructuresAsync(analysisId);
				var unmigrated = codeStructures.Where(cs => !cs.IsMigrated).ToList();

				if (!unmigrated.Any())
				{
					_logger.LogInformation("All code structures already migrated");
					return new List<CodeChunk>();
				}

				var maxTokens = (int)(MAX_TOKENS_PER_CHUNK * maxTokensPercentage) - CONTEXT_TOKENS_RESERVED;
				var chunks = new List<CodeChunk>();

				_logger.LogInformation($"Creating code chunks with max {maxTokens} tokens per chunk");

				var currentChunk = new CodeChunk { AnalysisId = analysisId };
				var currentTokenCount = 0;

				foreach (var structure in unmigrated.OrderBy(cs => cs.ComplexityScore))
				{
					var structureCode = FormatCodeStructureForMigration(structure);
					var estimatedTokens = EstimateTokens(structureCode);

					if (estimatedTokens > maxTokens)
					{
						var methodChunks = await CreateMethodChunksAsync(structure, maxTokens);
						chunks.AddRange(methodChunks);
						continue;
					}

					if (currentTokenCount + estimatedTokens > maxTokens && currentChunk.ClassIds.Any())
					{
						currentChunk.EstimatedTokens = currentTokenCount;
						chunks.Add(currentChunk);

						currentChunk = new CodeChunk { AnalysisId = analysisId };
						currentTokenCount = 0;
					}

					currentChunk.ClassIds.Add(structure.Id);
					currentChunk.CombinedCode += structureCode + "\n\n";
					currentTokenCount += estimatedTokens;
				}

				if (currentChunk.ClassIds.Any())
				{
					currentChunk.EstimatedTokens = currentTokenCount;
					chunks.Add(currentChunk);
				}

				foreach (var chunk in chunks)
				{
					await _repository.SaveCodeChunkAsync(chunk);
				}

				_logger.LogInformation($"Created {chunks.Count} code chunks");
				return chunks;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating code chunks");
				throw new MigrationException($"Failed to create code chunks: {ex.Message}", ex);
			}
		}

		public async Task<string> SendToClaudeAsync(string codeChunk, string context)
		{
			throw new NotImplementedException("This method is replaced by Claude Desktop MCP tools. Use 'process_migration_chunk' tool instead.");
		}

		private async Task<List<CodeChunk>> GetOrCreateCodeChunksAsync(string analysisId, double maxTokensPercentage)
		{
			var existingChunks = await _repository.GetCodeChunksAsync(analysisId);

			if (existingChunks.Any())
			{
				return existingChunks;
			}

			return await CreateCodeChunksAsync(analysisId, maxTokensPercentage);
		}

		private async Task<List<CodeChunk>> CreateMethodChunksAsync(CodeStructure structure, int maxTokens)
		{
			var chunks = new List<CodeChunk>();

			if (structure.Methods == null) return chunks;

			foreach (var method in structure.Methods.Where(m => !m.IsMigrated))
			{
				var methodCode = FormatMethodForMigration(method, structure);
				var estimatedTokens = EstimateTokens(methodCode);

				if (estimatedTokens <= maxTokens)
				{
					var chunk = new CodeChunk
					{
						AnalysisId = structure.AnalysisId,
						ClassIds = new List<string> { structure.Id },
						CombinedCode = methodCode,
						EstimatedTokens = estimatedTokens
					};

					chunks.Add(chunk);
				}
				else
				{
					_logger.LogWarning($"Method {method.Name} in {structure.ClassName} is too large ({estimatedTokens} tokens) and will be skipped");
				}
			}

			return chunks;
		}

		private string FormatCodeStructureForMigration(CodeStructure structure)
		{
			var sb = new StringBuilder();

			sb.AppendLine($"// === {structure.ClassName} ({structure.Type}) ===");
			sb.AppendLine($"// File: {structure.FilePath}");
			sb.AppendLine($"// Namespace: {structure.Namespace}");

			if (structure.DatabaseTables?.Any() == true)
			{
				sb.AppendLine($"// Database Tables: {string.Join(", ", structure.DatabaseTables)}");
			}

			sb.AppendLine($"// Complexity Score: {structure.ComplexityScore:F2}");
			sb.AppendLine();
			sb.AppendLine(structure.SourceCode ?? "// No source code available");

			return sb.ToString();
		}

		private string FormatMethodForMigration(MethodStructure method, CodeStructure parentClass)
		{
			var sb = new StringBuilder();

			sb.AppendLine($"// === Method: {method.Name} from {parentClass.ClassName} ===");
			sb.AppendLine($"// Return Type: {method.ReturnType}");
			sb.AppendLine($"// Has Database Access: {method.HasDatabaseAccess}");

			if (method.DatabaseOperations?.Any() == true)
			{
				sb.AppendLine($"// Database Operations: {string.Join(", ", method.DatabaseOperations)}");
			}

			sb.AppendLine($"// Complexity Score: {method.ComplexityScore:F2}");
			sb.AppendLine();
			sb.AppendLine("// Parent class context:");
			sb.AppendLine($"namespace {parentClass.Namespace}");
			sb.AppendLine("{");
			sb.AppendLine($"    class {parentClass.ClassName}");
			sb.AppendLine("    {");
			sb.AppendLine("        // ... other members ...");
			sb.AppendLine();
			sb.AppendLine("        " + (method.SourceCode ?? "// No source code available").Replace("\n", "\n        "));
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private int EstimateTokens(string text)
		{
			if (string.IsNullOrEmpty(text))
				return 0;

			return text.Length / AVERAGE_TOKENS_PER_CHARACTER;
		}
	}
}