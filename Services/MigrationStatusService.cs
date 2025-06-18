using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Dapper;
using Newtonsoft.Json;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Services
{
	public class MigrationStatusService : IMigrationStatusService
	{
		private readonly ILogger<MigrationStatusService> _logger;
		private readonly IDataRepository _repository;
		private readonly string _connectionString;

		public MigrationStatusService(ILogger<MigrationStatusService> logger, IDataRepository repository, string connectionString)
		{
			_logger = logger;
			_repository = repository;
			_connectionString = connectionString;
		}

		public async Task<MigrationStatus> GetStatusAsync(string analysisId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM migration_status WHERE analysis_id = @AnalysisId";
				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { AnalysisId = analysisId });

				if (row != null)
				{
					return new MigrationStatus
					{
						TotalItems = row.total_items,
						MigratedItems = row.migrated_items,
						PendingItems = row.pending_items,
						FailedItems = row.failed_items,
						ProgressPercentage = SafeConvertToDouble(row.progress_percentage),
						CurrentPhase = row.current_phase,
						Errors = JsonConvert.DeserializeObject<List<string>>(row.errors ?? "[]")
					};
				}

				return await CalculateCurrentStatusAsync(analysisId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting migration status for analysis {analysisId}");
				throw;
			}
		}

		public async Task UpdateStatusAsync(string analysisId, string phase, int processed, int total)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var progressPercentage = total > 0 ? (double)processed / total * 100.0 : 0.0;
				var pending = Math.Max(0, total - processed);

				var query = @"
                    INSERT INTO migration_status (analysis_id, total_items, migrated_items, pending_items, failed_items, 
                                                progress_percentage, current_phase, updated_at)
                    VALUES (@AnalysisId, @TotalItems, @MigratedItems, @PendingItems, @FailedItems, @ProgressPercentage::double precision, @CurrentPhase, @UpdatedAt)
                    ON CONFLICT (analysis_id) DO UPDATE SET
                        total_items = @TotalItems,
                        migrated_items = @MigratedItems,
                        pending_items = @PendingItems,
                        progress_percentage = @ProgressPercentage::double precision,
                        current_phase = @CurrentPhase,
                        updated_at = @UpdatedAt";

				await connection.ExecuteAsync(query, new
				{
					AnalysisId = analysisId,
					TotalItems = total,
					MigratedItems = processed,
					PendingItems = pending,
					FailedItems = 0,
					ProgressPercentage = progressPercentage,
					CurrentPhase = phase,
					UpdatedAt = DateTime.UtcNow
				});

				_logger.LogInformation($"Updated migration status for {analysisId}: {phase} - {processed}/{total} ({progressPercentage:F1}%)");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating migration status for analysis {analysisId}");
				throw;
			}
		}

		private async Task<MigrationStatus> CalculateCurrentStatusAsync(string analysisId)
		{
			try
			{
				var codeStructures = await _repository.GetCodeStructuresAsync(analysisId);
				var chunks = await _repository.GetCodeChunksAsync(analysisId);

				var totalItems = codeStructures.Count;
				var migratedItems = codeStructures.Count(cs => cs.IsMigrated);
				var pendingItems = totalItems - migratedItems;
				var progressPercentage = totalItems > 0 ? (double)migratedItems / totalItems * 100.0 : 0.0;

				return new MigrationStatus
				{
					TotalItems = totalItems,
					MigratedItems = migratedItems,
					PendingItems = pendingItems,
					FailedItems = 0,
					ProgressPercentage = progressPercentage,
					CurrentPhase = chunks.Any(c => !c.IsProcessed) ? "Migration In Progress" : "Migration Complete",
					Errors = new List<string>()
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error calculating current status for analysis {analysisId}");
				return new MigrationStatus
				{
					TotalItems = 0,
					MigratedItems = 0,
					PendingItems = 0,
					FailedItems = 1,
					ProgressPercentage = 0.0,
					CurrentPhase = "Error",
					Errors = new List<string> { ex.Message }
				};
			}
		}

		private static double SafeConvertToDouble(object value)
		{
			if (value == null) return 0.0;

			return value switch
			{
				double d => d,
				decimal dec => (double)dec,
				float f => (double)f,
				int i => (double)i,
				long l => (double)l,
				string s when double.TryParse(s, out var result) => result,
				_ => 0.0
			};
		}
	}
}