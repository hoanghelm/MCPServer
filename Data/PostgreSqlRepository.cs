using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Newtonsoft.Json;
using Dapper;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Data
{
	public class PostgreSqlRepository : IDataRepository
	{
		private readonly ILogger<PostgreSqlRepository> _logger;
		private readonly string _connectionString;

		public PostgreSqlRepository(ILogger<PostgreSqlRepository> logger, IOptions<DatabaseOptions> options)
		{
			_logger = logger;
			_connectionString = options.Value.ConnectionString;
			MigrateAsync().Wait();
		}

		// Project operations
		public async Task<string> SaveWorkspaceAnalysisAsync(WorkspaceAnalysis analysis)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				// Save analysis
				var analysisQuery = @"
                    INSERT INTO workspace_analysis (project_id, project_name, workspace_path, analyzed_at, total_files, 
                                                   files_by_type, files_by_complexity)
                    VALUES (@ProjectId, @ProjectName, @WorkspacePath, @AnalyzedAt, @TotalFiles, 
                            @FilesByType::jsonb, @FilesByComplexity::jsonb)
                    ON CONFLICT (project_id) DO UPDATE SET
                        project_name = @ProjectName,
                        workspace_path = @WorkspacePath,
                        analyzed_at = @AnalyzedAt,
                        total_files = @TotalFiles,
                        files_by_type = @FilesByType::jsonb,
                        files_by_complexity = @FilesByComplexity::jsonb";

				await connection.ExecuteAsync(analysisQuery, new
				{
					analysis.ProjectId,
					analysis.ProjectName,
					analysis.WorkspacePath,
					analysis.AnalyzedAt,
					analysis.TotalFiles,
					FilesByType = JsonConvert.SerializeObject(analysis.FilesByType),
					FilesByComplexity = JsonConvert.SerializeObject(analysis.FilesByComplexity)
				});

				// Save all files
				var allFiles = analysis.CsFiles
					.Concat(analysis.AspxFiles)
					.Concat(analysis.AscxFiles);

				foreach (var file in allFiles)
				{
					await SaveProjectFileAsync(connection, file);
				}

				_logger.LogInformation($"Saved workspace analysis for {analysis.ProjectName}");
				return analysis.ProjectId;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving workspace analysis");
				throw;
			}
		}

		public async Task<WorkspaceAnalysis> GetWorkspaceAnalysisAsync(string projectId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM workspace_analysis WHERE project_id = @ProjectId";
				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { ProjectId = projectId });

				if (row == null)
					return null;

				var analysis = new WorkspaceAnalysis
				{
					ProjectId = row.project_id,
					ProjectName = row.project_name,
					WorkspacePath = row.workspace_path,
					AnalyzedAt = row.analyzed_at,
					TotalFiles = row.total_files,
					FilesByType = JsonConvert.DeserializeObject<Dictionary<string, int>>(row.files_by_type ?? "{}"),
					FilesByComplexity = JsonConvert.DeserializeObject<Dictionary<string, int>>(row.files_by_complexity ?? "{}")
				};

				// Load files
				var files = await GetProjectFilesAsync(projectId);
				foreach (var file in files)
				{
					if (file.FilePath.EndsWith(".cs"))
						analysis.CsFiles.Add(file);
					else if (file.FilePath.EndsWith(".aspx"))
						analysis.AspxFiles.Add(file);
					else if (file.FilePath.EndsWith(".ascx"))
						analysis.AscxFiles.Add(file);

					// Categorize by type
					switch (file.FileType)
					{
						case "WebForm":
						case "CodeBehind":
							analysis.WebForms.Add(file);
							break;
						case "DataAccess":
							analysis.DataAccessFiles.Add(file);
							break;
						case "BusinessLogic":
							analysis.BusinessLogicFiles.Add(file);
							break;
					}
				}

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting workspace analysis {projectId}");
				throw;
			}
		}

		public async Task<MigrationProject> GetMigrationProjectAsync(string projectId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM migration_projects WHERE project_id = @ProjectId";
				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { ProjectId = projectId });

				if (row == null)
					return null;

				return new MigrationProject
				{
					ProjectId = row.project_id,
					ProjectName = row.project_name,
					WorkspacePath = row.workspace_path,
					DalProjectPath = row.dal_project_path,
					BalProjectPath = row.bal_project_path,
					CreatedAt = row.created_at,
					Status = Enum.Parse<ProjectStatus>(row.status)
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting migration project {projectId}");
				throw;
			}
		}

		public async Task SaveMigrationProjectAsync(MigrationProject project)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"
                    INSERT INTO migration_projects (project_id, project_name, workspace_path, dal_project_path, 
                                                   bal_project_path, created_at, status)
                    VALUES (@ProjectId, @ProjectName, @WorkspacePath, @DalProjectPath, 
                            @BalProjectPath, @CreatedAt, @Status)
                    ON CONFLICT (project_id) DO UPDATE SET
                        project_name = @ProjectName,
                        workspace_path = @WorkspacePath,
                        dal_project_path = @DalProjectPath,
                        bal_project_path = @BalProjectPath,
                        status = @Status";

				await connection.ExecuteAsync(query, new
				{
					project.ProjectId,
					project.ProjectName,
					project.WorkspacePath,
					project.DalProjectPath,
					project.BalProjectPath,
					project.CreatedAt,
					Status = project.Status.ToString()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving migration project");
				throw;
			}
		}

		// File operations
		public async Task<List<ProjectFile>> GetProjectFilesAsync(string projectId, string statusFilter = "all")
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM project_files WHERE project_id = @ProjectId";

				if (statusFilter != "all")
				{
					query += " AND migration_status = @Status";
				}

				query += " ORDER BY file_path";

				var parameters = new DynamicParameters();
				parameters.Add("ProjectId", projectId);
				if (statusFilter != "all")
				{
					var status = statusFilter switch
					{
						"migrated" => MigrationStatus.Completed.ToString(),
						"pending" => MigrationStatus.Pending.ToString(),
						"failed" => MigrationStatus.Failed.ToString(),
						_ => statusFilter
					};
					parameters.Add("Status", status);
				}

				var rows = await connection.QueryAsync<dynamic>(query, parameters);

				var files = new List<ProjectFile>();
				foreach (var row in rows)
				{
					var file = new ProjectFile
					{
						Id = row.id,
						ProjectId = row.project_id,
						FilePath = row.file_path,
						FileName = row.file_name,
						FileType = row.file_type,
						SourceCode = row.source_code,
						MigrationStatus = Enum.Parse<MigrationStatus>(row.migration_status),
						MigratedAt = row.migrated_at,
						DalOutputPath = row.dal_output_path,
						BalOutputPath = row.bal_output_path,
						ErrorMessage = row.error_message,
						Complexity = row.complexity,
						Classes = JsonConvert.DeserializeObject<List<string>>(row.classes ?? "[]"),
						Dependencies = JsonConvert.DeserializeObject<List<string>>(row.dependencies ?? "[]"),
						MigratedCode = JsonConvert.DeserializeObject<Dictionary<string, string>>(row.migrated_code ?? "{}")
					};
					files.Add(file);
				}

				return files;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting project files for {projectId}");
				throw;
			}
		}

		public async Task<ProjectFile> GetNextUnmigratedFileAsync(string projectId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"SELECT * FROM project_files 
                             WHERE project_id = @ProjectId 
                             AND migration_status = @Status
                             AND file_type NOT IN ('Unknown', 'Model')
                             ORDER BY complexity DESC, file_path
                             LIMIT 1";

				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new
				{
					ProjectId = projectId,
					Status = MigrationStatus.Pending.ToString()
				});

				if (row == null)
					return null;

				return new ProjectFile
				{
					Id = row.id,
					ProjectId = row.project_id,
					FilePath = row.file_path,
					FileName = row.file_name,
					FileType = row.file_type,
					SourceCode = row.source_code,
					MigrationStatus = Enum.Parse<MigrationStatus>(row.migration_status),
					Complexity = row.complexity,
					Classes = JsonConvert.DeserializeObject<List<string>>(row.classes ?? "[]"),
					Dependencies = JsonConvert.DeserializeObject<List<string>>(row.dependencies ?? "[]")
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting next unmigrated file for {projectId}");
				throw;
			}
		}

		public async Task UpdateFileStatusAsync(string fileId, MigrationStatus status, string error = null)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"UPDATE project_files 
                             SET migration_status = @Status, 
                                 error_message = @ErrorMessage,
                                 updated_at = CURRENT_TIMESTAMP
                             WHERE id = @FileId";

				await connection.ExecuteAsync(query, new
				{
					Status = status.ToString(),
					ErrorMessage = error,
					FileId = fileId
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating file status for {fileId}");
				throw;
			}
		}

		public async Task SaveMigratedFileAsync(ProjectFile file)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"UPDATE project_files 
                             SET migration_status = @Status,
                                 migrated_at = @MigratedAt,
                                 dal_output_path = @DalOutputPath,
                                 bal_output_path = @BalOutputPath,
                                 migrated_code = @MigratedCode::jsonb,
                                 updated_at = CURRENT_TIMESTAMP
                             WHERE id = @Id";

				await connection.ExecuteAsync(query, new
				{
					Status = file.MigrationStatus.ToString(),
					file.MigratedAt,
					file.DalOutputPath,
					file.BalOutputPath,
					MigratedCode = JsonConvert.SerializeObject(file.MigratedCode),
					file.Id
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error saving migrated file {file.Id}");
				throw;
			}
		}

		// Status operations
		public async Task<MigrationProgress> GetMigrationStatusAsync(string projectId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				// Get project info
				var project = await GetMigrationProjectAsync(projectId);

				// Get file counts
				var query = @"SELECT 
                    COUNT(*) as total,
                    COUNT(CASE WHEN migration_status = 'Completed' THEN 1 END) as completed,
                    COUNT(CASE WHEN migration_status = 'Failed' THEN 1 END) as failed,
                    COUNT(CASE WHEN migration_status = 'Pending' THEN 1 END) as pending,
                    MIN(CASE WHEN migration_status = 'Completed' THEN migrated_at END) as first_migrated,
                    MAX(CASE WHEN migration_status = 'Completed' THEN migrated_at END) as last_migrated
                FROM project_files 
                WHERE project_id = @ProjectId 
                AND file_type NOT IN ('Unknown', 'Model')";

				var stats = await connection.QueryFirstAsync<dynamic>(query, new { ProjectId = projectId });

				var progress = new MigrationProgress
				{
					ProjectId = projectId,
					CurrentStatus = project?.Status.ToString() ?? "Unknown",
					TotalFiles = (int)stats.total,
					MigratedFiles = (int)stats.completed,
					FailedFiles = (int)stats.failed,
					PendingFiles = (int)stats.pending,
					DalProjectPath = project?.DalProjectPath,
					BalProjectPath = project?.BalProjectPath,
					StartedAt = stats.first_migrated,
					LastUpdated = stats.last_migrated
				};

				progress.ProgressPercentage = progress.TotalFiles > 0
					? (double)progress.MigratedFiles / progress.TotalFiles * 100
					: 0;

				// Estimate completion
				if (progress.MigratedFiles > 0 && progress.PendingFiles > 0 &&
					progress.StartedAt.HasValue && progress.LastUpdated.HasValue)
				{
					var elapsedTime = progress.LastUpdated.Value - progress.StartedAt.Value;
					var avgTimePerFile = elapsedTime.TotalMinutes / progress.MigratedFiles;
					var estimatedMinutesRemaining = avgTimePerFile * progress.PendingFiles;
					progress.EstimatedCompletion = DateTime.UtcNow.AddMinutes(estimatedMinutesRemaining);
				}

				return progress;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting migration status for {projectId}");
				throw;
			}
		}

		public async Task UpdateMigrationProgressAsync(string projectId)
		{
			try
			{
				// Get current status
				var status = await GetMigrationStatusAsync(projectId);

				// Update project status if needed
				if (status.PendingFiles == 0 && status.TotalFiles > 0)
				{
					using var connection = new NpgsqlConnection(_connectionString);
					await connection.OpenAsync();

					var newStatus = status.FailedFiles > 0 ? ProjectStatus.Failed : ProjectStatus.Completed;

					await connection.ExecuteAsync(
						"UPDATE migration_projects SET status = @Status WHERE project_id = @ProjectId",
						new { Status = newStatus.ToString(), ProjectId = projectId });
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating migration progress for {projectId}");
				throw;
			}
		}

		// Database operations
		public async Task<bool> TestConnectionAsync()
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				// Test with a simple query
				var result = await connection.ExecuteScalarAsync<int>("SELECT 1");

				_logger.LogInformation("Database connection test successful");
				return result == 1;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Database connection test failed");
				return false;
			}
		}

		public async Task MigrateAsync()
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var createTablesScript = @"
                    -- Workspace Analysis Table
                    CREATE TABLE IF NOT EXISTS workspace_analysis (
                        project_id VARCHAR(50) PRIMARY KEY,
                        project_name VARCHAR(200) NOT NULL,
                        workspace_path TEXT NOT NULL,
                        analyzed_at TIMESTAMP NOT NULL,
                        total_files INTEGER NOT NULL,
                        files_by_type JSONB,
                        files_by_complexity JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Migration Projects Table
                    CREATE TABLE IF NOT EXISTS migration_projects (
                        project_id VARCHAR(50) PRIMARY KEY,
                        project_name VARCHAR(200) NOT NULL,
                        workspace_path TEXT NOT NULL,
                        dal_project_path TEXT,
                        bal_project_path TEXT,
                        created_at TIMESTAMP NOT NULL,
                        status VARCHAR(50) NOT NULL,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Project Files Table
                    CREATE TABLE IF NOT EXISTS project_files (
                        id VARCHAR(50) PRIMARY KEY,
                        project_id VARCHAR(50) NOT NULL,
                        file_path TEXT NOT NULL,
                        file_name VARCHAR(200) NOT NULL,
                        file_type VARCHAR(50) NOT NULL,
                        source_code TEXT,
                        migration_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
                        migrated_at TIMESTAMP,
                        dal_output_path TEXT,
                        bal_output_path TEXT,
                        error_message TEXT,
                        complexity INTEGER DEFAULT 1,
                        classes JSONB,
                        dependencies JSONB,
                        migrated_code JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Create indexes
                    CREATE INDEX IF NOT EXISTS idx_project_files_project_id ON project_files(project_id);
                    CREATE INDEX IF NOT EXISTS idx_project_files_status ON project_files(migration_status);
                    CREATE INDEX IF NOT EXISTS idx_project_files_type ON project_files(file_type);
                    CREATE INDEX IF NOT EXISTS idx_workspace_analysis_project_name ON workspace_analysis(project_name);
                    CREATE INDEX IF NOT EXISTS idx_migration_projects_status ON migration_projects(status);

                    -- Create update trigger for updated_at
                    CREATE OR REPLACE FUNCTION update_updated_at_column()
                    RETURNS TRIGGER AS $$
                    BEGIN
                        NEW.updated_at = CURRENT_TIMESTAMP;
                        RETURN NEW;
                    END;
                    $$ language 'plpgsql';

                    -- Apply trigger to tables
                    DROP TRIGGER IF EXISTS update_project_files_updated_at ON project_files;
                    CREATE TRIGGER update_project_files_updated_at 
                        BEFORE UPDATE ON project_files 
                        FOR EACH ROW 
                        EXECUTE FUNCTION update_updated_at_column();

                    DROP TRIGGER IF EXISTS update_migration_projects_updated_at ON migration_projects;
                    CREATE TRIGGER update_migration_projects_updated_at 
                        BEFORE UPDATE ON migration_projects 
                        FOR EACH ROW 
                        EXECUTE FUNCTION update_updated_at_column();
                ";

				await connection.ExecuteAsync(createTablesScript);
				_logger.LogInformation("Database schema initialized successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error initializing database schema");
				throw;
			}
		}

		// Helper methods
		private async Task SaveProjectFileAsync(NpgsqlConnection connection, ProjectFile file)
		{
			var query = @"
                INSERT INTO project_files (id, project_id, file_path, file_name, file_type, source_code,
                                         migration_status, complexity, classes, dependencies)
                VALUES (@Id, @ProjectId, @FilePath, @FileName, @FileType, @SourceCode,
                        @MigrationStatus, @Complexity, @Classes::jsonb, @Dependencies::jsonb)
                ON CONFLICT (id) DO UPDATE SET
                    file_path = @FilePath,
                    file_name = @FileName,
                    file_type = @FileType,
                    source_code = @SourceCode,
                    migration_status = @MigrationStatus,
                    complexity = @Complexity,
                    classes = @Classes::jsonb,
                    dependencies = @Dependencies::jsonb,
                    updated_at = CURRENT_TIMESTAMP";

			await connection.ExecuteAsync(query, new
			{
				file.Id,
				file.ProjectId,
				file.FilePath,
				file.FileName,
				file.FileType,
				file.SourceCode,
				MigrationStatus = file.MigrationStatus.ToString(),
				file.Complexity,
				Classes = JsonConvert.SerializeObject(file.Classes),
				Dependencies = JsonConvert.SerializeObject(file.Dependencies)
			});
		}
	}
}