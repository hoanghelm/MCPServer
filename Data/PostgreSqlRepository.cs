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
			InitializeDatabaseAsync().Wait();
		}

		public async Task<string> SaveAnalysisResultAsync(AnalysisResult result)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var analysisQuery = @"
                    INSERT INTO analysis_results (id, directory_path, analyzed_at, total_files, total_classes, total_methods, complexity_score, dependencies)
                    VALUES (@Id, @DirectoryPath, @AnalyzedAt, @TotalFiles, @TotalClasses, @TotalMethods, @ComplexityScore::double precision, @Dependencies::jsonb)
                    ON CONFLICT (id) DO UPDATE SET
                        directory_path = @DirectoryPath,
                        analyzed_at = @AnalyzedAt,
                        total_files = @TotalFiles,
                        total_classes = @TotalClasses,
                        total_methods = @TotalMethods,
                        complexity_score = @ComplexityScore::double precision,
                        dependencies = @Dependencies::jsonb";

				await connection.ExecuteAsync(analysisQuery, new
				{
					result.Id,
					result.DirectoryPath,
					result.AnalyzedAt,
					result.TotalFiles,
					result.TotalClasses,
					result.TotalMethods,
					ComplexityScore = result.ComplexityScore,
					Dependencies = JsonConvert.SerializeObject(result.Dependencies)
				});

				foreach (var structure in result.CodeStructures)
				{
					await SaveCodeStructureInternalAsync(connection, structure);
				}

				_logger.LogInformation($"Saved analysis result {result.Id} with {result.CodeStructures.Count} code structures");
				return result.Id;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error saving analysis result {result.Id}");
				throw;
			}
		}

		public async Task<AnalysisResult> GetAnalysisResultAsync(string analysisId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM analysis_results WHERE id = @AnalysisId";
				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { AnalysisId = analysisId });

				if (row == null)
					return null;

				var result = new AnalysisResult
				{
					Id = row.id,
					DirectoryPath = row.directory_path,
					AnalyzedAt = row.analyzed_at,
					TotalFiles = row.total_files,
					TotalClasses = row.total_classes,
					TotalMethods = row.total_methods,
					ComplexityScore = SafeConvertToDouble(row.complexity_score),
					Dependencies = JsonConvert.DeserializeObject<List<string>>(row.dependencies ?? "[]")
				};

				result.CodeStructures = await GetCodeStructuresAsync(analysisId);
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting analysis result {analysisId}");
				throw;
			}
		}

		public async Task<List<CodeStructure>> GetCodeStructuresAsync(string analysisId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var structuresQuery = "SELECT * FROM code_structures WHERE analysis_id = @AnalysisId ORDER BY class_name";
				var structures = await connection.QueryAsync<dynamic>(structuresQuery, new { AnalysisId = analysisId });

				var result = new List<CodeStructure>();

				foreach (var row in structures)
				{
					var structure = new CodeStructure
					{
						Id = row.id,
						AnalysisId = row.analysis_id,
						FilePath = row.file_path,
						ClassName = row.class_name,
						Namespace = row.namespace_name,
						Type = Enum.Parse<CodeStructureType>(row.structure_type),
						SourceCode = row.source_code,
						IsMigrated = row.is_migrated,
						MigratedAt = row.migrated_at,
						MigratedCode = row.migrated_code,
						ComplexityScore = SafeConvertToDouble(row.complexity_score),
						Dependencies = JsonConvert.DeserializeObject<List<string>>(row.dependencies ?? "[]"),
						DatabaseTables = JsonConvert.DeserializeObject<List<string>>(row.database_tables ?? "[]")
					};

					var methodsQuery = "SELECT * FROM method_structures WHERE code_structure_id = @StructureId ORDER BY name";
					var methods = await connection.QueryAsync<dynamic>(methodsQuery, new { StructureId = structure.Id });

					foreach (var methodRow in methods)
					{
						var method = new MethodStructure
						{
							Id = methodRow.id,
							Name = methodRow.name,
							ReturnType = methodRow.return_type,
							LinesOfCode = methodRow.lines_of_code,
							ComplexityScore = SafeConvertToDouble(methodRow.complexity_score),
							SourceCode = methodRow.source_code,
							HasDatabaseAccess = methodRow.has_database_access,
							IsMigrated = methodRow.is_migrated,
							MigratedCode = methodRow.migrated_code,
							Dependencies = JsonConvert.DeserializeObject<List<string>>(methodRow.dependencies ?? "[]"),
							DatabaseOperations = JsonConvert.DeserializeObject<List<string>>(methodRow.database_operations ?? "[]"),
							Parameters = JsonConvert.DeserializeObject<List<ParameterStructure>>(methodRow.parameters ?? "[]")
						};

						structure.Methods.Add(method);
					}

					var propertiesQuery = "SELECT * FROM property_structures WHERE code_structure_id = @StructureId ORDER BY name";
					var properties = await connection.QueryAsync<dynamic>(propertiesQuery, new { StructureId = structure.Id });

					foreach (var propRow in properties)
					{
						var property = new PropertyStructure
						{
							Name = propRow.name,
							Type = propRow.property_type,
							HasGetter = propRow.has_getter,
							HasSetter = propRow.has_setter,
							IsPublic = propRow.is_public
						};

						structure.Properties.Add(property);
					}

					result.Add(structure);
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting code structures for analysis {analysisId}");
				throw;
			}
		}

		public async Task UpdateCodeStructureAsync(CodeStructure structure)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"
                    UPDATE code_structures SET
                        is_migrated = @IsMigrated,
                        migrated_at = @MigratedAt,
                        migrated_code = @MigratedCode
                    WHERE id = @Id";

				await connection.ExecuteAsync(query, new
				{
					structure.IsMigrated,
					structure.MigratedAt,
					structure.MigratedCode,
					structure.Id
				});

				foreach (var method in structure.Methods)
				{
					var methodQuery = @"
                        UPDATE method_structures SET
                            is_migrated = @IsMigrated,
                            migrated_code = @MigratedCode
                        WHERE id = @Id";

					await connection.ExecuteAsync(methodQuery, new
					{
						method.IsMigrated,
						method.MigratedCode,
						method.Id
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating code structure {structure.Id}");
				throw;
			}
		}

		public async Task SaveMigrationStructureAsync(MigrationStructure structure)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"
                    INSERT INTO migration_structures (analysis_id, business_logic_interfaces, data_access_interfaces, models, suggested_projects, created_at)
                    VALUES (@AnalysisId, @BusinessLogicInterfaces::jsonb, @DataAccessInterfaces::jsonb, @Models::jsonb, @SuggestedProjects::jsonb, @CreatedAt)
                    ON CONFLICT (analysis_id) DO UPDATE SET
                        business_logic_interfaces = @BusinessLogicInterfaces::jsonb,
                        data_access_interfaces = @DataAccessInterfaces::jsonb,
                        models = @Models::jsonb,
                        suggested_projects = @SuggestedProjects::jsonb,
                        updated_at = @CreatedAt";

				await connection.ExecuteAsync(query, new
				{
					structure.AnalysisId,
					BusinessLogicInterfaces = JsonConvert.SerializeObject(structure.BusinessLogicInterfaces),
					DataAccessInterfaces = JsonConvert.SerializeObject(structure.DataAccessInterfaces),
					Models = JsonConvert.SerializeObject(structure.Models),
					SuggestedProjects = JsonConvert.SerializeObject(structure.SuggestedProjects),
					CreatedAt = DateTime.UtcNow
				});

				_logger.LogInformation($"Saved migration structure for analysis {structure.AnalysisId}");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error saving migration structure for analysis {structure.AnalysisId}");
				throw;
			}
		}

		public async Task<MigrationStructure> GetMigrationStructureAsync(string analysisId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM migration_structures WHERE analysis_id = @AnalysisId";
				var row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { AnalysisId = analysisId });

				if (row == null)
					return null;

				return new MigrationStructure
				{
					AnalysisId = row.analysis_id,
					BusinessLogicInterfaces = JsonConvert.DeserializeObject<List<InterfaceStructure>>(row.business_logic_interfaces ?? "[]"),
					DataAccessInterfaces = JsonConvert.DeserializeObject<List<InterfaceStructure>>(row.data_access_interfaces ?? "[]"),
					Models = JsonConvert.DeserializeObject<List<ModelStructure>>(row.models ?? "[]"),
					SuggestedProjects = JsonConvert.DeserializeObject<List<string>>(row.suggested_projects ?? "[]")
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting migration structure for analysis {analysisId}");
				throw;
			}
		}

		public async Task SaveCodeChunkAsync(CodeChunk chunk)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"
                    INSERT INTO code_chunks (id, analysis_id, class_ids, combined_code, estimated_tokens, is_processed, migrated_code, errors, created_at)
                    VALUES (@Id, @AnalysisId, @ClassIds::jsonb, @CombinedCode, @EstimatedTokens, @IsProcessed, @MigratedCode, @Errors::jsonb, @CreatedAt)
                    ON CONFLICT (id) DO UPDATE SET
                        combined_code = @CombinedCode,
                        estimated_tokens = @EstimatedTokens,
                        is_processed = @IsProcessed,
                        migrated_code = @MigratedCode,
                        errors = @Errors::jsonb,
                        updated_at = @CreatedAt";

				await connection.ExecuteAsync(query, new
				{
					chunk.Id,
					chunk.AnalysisId,
					ClassIds = JsonConvert.SerializeObject(chunk.ClassIds),
					chunk.CombinedCode,
					chunk.EstimatedTokens,
					chunk.IsProcessed,
					chunk.MigratedCode,
					Errors = JsonConvert.SerializeObject(chunk.Errors),
					CreatedAt = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error saving code chunk {chunk.Id}");
				throw;
			}
		}

		public async Task<List<CodeChunk>> GetCodeChunksAsync(string analysisId)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = "SELECT * FROM code_chunks WHERE analysis_id = @AnalysisId ORDER BY created_at";
				var rows = await connection.QueryAsync<dynamic>(query, new { AnalysisId = analysisId });

				var result = new List<CodeChunk>();

				foreach (var row in rows)
				{
					var chunk = new CodeChunk
					{
						Id = row.id,
						AnalysisId = row.analysis_id,
						ClassIds = JsonConvert.DeserializeObject<List<string>>(row.class_ids ?? "[]"),
						CombinedCode = row.combined_code,
						EstimatedTokens = row.estimated_tokens,
						IsProcessed = row.is_processed,
						MigratedCode = row.migrated_code,
						Errors = JsonConvert.DeserializeObject<List<string>>(row.errors ?? "[]")
					};

					result.Add(chunk);
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error getting code chunks for analysis {analysisId}");
				throw;
			}
		}

		public async Task UpdateCodeChunkAsync(CodeChunk chunk)
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var query = @"
                    UPDATE code_chunks SET
                        is_processed = @IsProcessed,
                        migrated_code = @MigratedCode,
                        errors = @Errors::jsonb,
                        updated_at = @UpdatedAt
                    WHERE id = @Id";

				await connection.ExecuteAsync(query, new
				{
					chunk.IsProcessed,
					chunk.MigratedCode,
					Errors = JsonConvert.SerializeObject(chunk.Errors),
					UpdatedAt = DateTime.UtcNow,
					chunk.Id
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error updating code chunk {chunk.Id}");
				throw;
			}
		}

		public async Task<bool> TestConnectionAsync()
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();
				_logger.LogInformation("Database connection test successful");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Database connection test failed");
				return false;
			}
		}

		private async Task SaveCodeStructureInternalAsync(NpgsqlConnection connection, CodeStructure structure)
		{
			var structureQuery = @"
                INSERT INTO code_structures (id, analysis_id, file_path, class_name, namespace_name, structure_type, source_code, 
                                           is_migrated, migrated_at, migrated_code, complexity_score, dependencies, database_tables)
                VALUES (@Id, @AnalysisId, @FilePath, @ClassName, @NamespaceName, @StructureType, @SourceCode, 
                        @IsMigrated, @MigratedAt, @MigratedCode, @ComplexityScore::double precision, @Dependencies::jsonb, @DatabaseTables::jsonb)
                ON CONFLICT (id) DO UPDATE SET
                    file_path = @FilePath,
                    class_name = @ClassName,
                    namespace_name = @NamespaceName,
                    structure_type = @StructureType,
                    source_code = @SourceCode,
                    is_migrated = @IsMigrated,
                    migrated_at = @MigratedAt,
                    migrated_code = @MigratedCode,
                    complexity_score = @ComplexityScore::double precision,
                    dependencies = @Dependencies::jsonb,
                    database_tables = @DatabaseTables::jsonb";

			await connection.ExecuteAsync(structureQuery, new
			{
				structure.Id,
				structure.AnalysisId,
				structure.FilePath,
				structure.ClassName,
				NamespaceName = structure.Namespace,
				StructureType = structure.Type.ToString(),
				structure.SourceCode,
				structure.IsMigrated,
				structure.MigratedAt,
				structure.MigratedCode,
				ComplexityScore = structure.ComplexityScore,
				Dependencies = JsonConvert.SerializeObject(structure.Dependencies),
				DatabaseTables = JsonConvert.SerializeObject(structure.DatabaseTables)
			});

			foreach (var method in structure.Methods)
			{
				var methodQuery = @"
                    INSERT INTO method_structures (id, code_structure_id, name, return_type, lines_of_code, complexity_score, 
                                                 source_code, has_database_access, is_migrated, migrated_code, dependencies, 
                                                 database_operations, parameters)
                    VALUES (@Id, @CodeStructureId, @Name, @ReturnType, @LinesOfCode, @ComplexityScore::double precision, @SourceCode, 
                            @HasDatabaseAccess, @IsMigrated, @MigratedCode, @Dependencies::jsonb, @DatabaseOperations::jsonb, @Parameters::jsonb)
                    ON CONFLICT (id) DO UPDATE SET
                        name = @Name,
                        return_type = @ReturnType,
                        lines_of_code = @LinesOfCode,
                        complexity_score = @ComplexityScore::double precision,
                        source_code = @SourceCode,
                        has_database_access = @HasDatabaseAccess,
                        is_migrated = @IsMigrated,
                        migrated_code = @MigratedCode,
                        dependencies = @Dependencies::jsonb,
                        database_operations = @DatabaseOperations::jsonb,
                        parameters = @Parameters::jsonb";

				await connection.ExecuteAsync(methodQuery, new
				{
					method.Id,
					CodeStructureId = structure.Id,
					method.Name,
					method.ReturnType,
					method.LinesOfCode,
					ComplexityScore = method.ComplexityScore,
					method.SourceCode,
					method.HasDatabaseAccess,
					method.IsMigrated,
					method.MigratedCode,
					Dependencies = JsonConvert.SerializeObject(method.Dependencies),
					DatabaseOperations = JsonConvert.SerializeObject(method.DatabaseOperations),
					Parameters = JsonConvert.SerializeObject(method.Parameters)
				});
			}

			foreach (var property in structure.Properties)
			{
				var propertyQuery = @"
                    INSERT INTO property_structures (code_structure_id, name, property_type, has_getter, has_setter, is_public)
                    VALUES (@CodeStructureId, @Name, @PropertyType, @HasGetter, @HasSetter, @IsPublic)
                    ON CONFLICT (code_structure_id, name) DO UPDATE SET
                        property_type = @PropertyType,
                        has_getter = @HasGetter,
                        has_setter = @HasSetter,
                        is_public = @IsPublic";

				await connection.ExecuteAsync(propertyQuery, new
				{
					CodeStructureId = structure.Id,
					property.Name,
					PropertyType = property.Type,
					property.HasGetter,
					property.HasSetter,
					property.IsPublic
				});
			}
		}

		private async Task InitializeDatabaseAsync()
		{
			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				await connection.OpenAsync();

				var createTablesScript = @"
                    -- Analysis Results Table
                    CREATE TABLE IF NOT EXISTS analysis_results (
                        id VARCHAR(50) PRIMARY KEY,
                        directory_path TEXT NOT NULL,
                        analyzed_at TIMESTAMP NOT NULL,
                        total_files INTEGER NOT NULL,
                        total_classes INTEGER NOT NULL,
                        total_methods INTEGER NOT NULL,
                        complexity_score DOUBLE PRECISION NOT NULL DEFAULT 0,
                        dependencies JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Code Structures Table
                    CREATE TABLE IF NOT EXISTS code_structures (
                        id VARCHAR(50) PRIMARY KEY,
                        analysis_id VARCHAR(50) NOT NULL REFERENCES analysis_results(id) ON DELETE CASCADE,
                        file_path TEXT NOT NULL,
                        class_name VARCHAR(200),
                        namespace_name VARCHAR(500),
                        structure_type VARCHAR(50) NOT NULL,
                        source_code TEXT,
                        is_migrated BOOLEAN DEFAULT FALSE,
                        migrated_at TIMESTAMP,
                        migrated_code TEXT,
                        complexity_score DOUBLE PRECISION DEFAULT 0,
                        dependencies JSONB,
                        database_tables JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Method Structures Table
                    CREATE TABLE IF NOT EXISTS method_structures (
                        id VARCHAR(50) PRIMARY KEY,
                        code_structure_id VARCHAR(50) NOT NULL REFERENCES code_structures(id) ON DELETE CASCADE,
                        name VARCHAR(200) NOT NULL,
                        return_type VARCHAR(200),
                        lines_of_code INTEGER DEFAULT 0,
                        complexity_score DOUBLE PRECISION DEFAULT 0,
                        source_code TEXT,
                        has_database_access BOOLEAN DEFAULT FALSE,
                        is_migrated BOOLEAN DEFAULT FALSE,
                        migrated_code TEXT,
                        dependencies JSONB,
                        database_operations JSONB,
                        parameters JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Property Structures Table
                    CREATE TABLE IF NOT EXISTS property_structures (
                        code_structure_id VARCHAR(50) NOT NULL REFERENCES code_structures(id) ON DELETE CASCADE,
                        name VARCHAR(200) NOT NULL,
                        property_type VARCHAR(200) NOT NULL,
                        has_getter BOOLEAN DEFAULT FALSE,
                        has_setter BOOLEAN DEFAULT FALSE,
                        is_public BOOLEAN DEFAULT FALSE,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (code_structure_id, name)
                    );

                    -- Migration Structures Table
                    CREATE TABLE IF NOT EXISTS migration_structures (
                        analysis_id VARCHAR(50) PRIMARY KEY REFERENCES analysis_results(id) ON DELETE CASCADE,
                        business_logic_interfaces JSONB,
                        data_access_interfaces JSONB,
                        models JSONB,
                        suggested_projects JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Code Chunks Table
                    CREATE TABLE IF NOT EXISTS code_chunks (
                        id VARCHAR(50) PRIMARY KEY,
                        analysis_id VARCHAR(50) NOT NULL REFERENCES analysis_results(id) ON DELETE CASCADE,
                        class_ids JSONB,
                        combined_code TEXT,
                        estimated_tokens INTEGER DEFAULT 0,
                        is_processed BOOLEAN DEFAULT FALSE,
                        migrated_code TEXT,
                        errors JSONB,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Migration Status Table
                    CREATE TABLE IF NOT EXISTS migration_status (
                        analysis_id VARCHAR(50) PRIMARY KEY REFERENCES analysis_results(id) ON DELETE CASCADE,
                        total_items INTEGER DEFAULT 0,
                        migrated_items INTEGER DEFAULT 0,
                        pending_items INTEGER DEFAULT 0,
                        failed_items INTEGER DEFAULT 0,
                        progress_percentage DOUBLE PRECISION DEFAULT 0,
                        current_phase VARCHAR(100),
                        errors JSONB,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Create indexes for better performance
                    CREATE INDEX IF NOT EXISTS idx_code_structures_analysis_id ON code_structures(analysis_id);
                    CREATE INDEX IF NOT EXISTS idx_method_structures_code_structure_id ON method_structures(code_structure_id);
                    CREATE INDEX IF NOT EXISTS idx_property_structures_code_structure_id ON property_structures(code_structure_id);
                    CREATE INDEX IF NOT EXISTS idx_code_chunks_analysis_id ON code_chunks(analysis_id);
                    CREATE INDEX IF NOT EXISTS idx_code_chunks_is_processed ON code_chunks(is_processed);
                    CREATE INDEX IF NOT EXISTS idx_code_structures_is_migrated ON code_structures(is_migrated);
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