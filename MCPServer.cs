using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP
{
	public class MCPServer
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<MCPServer> _logger;
		private readonly Dictionary<string, Func<JObject, Task<object>>> _tools;

		public MCPServer(IServiceProvider serviceProvider, ILogger<MCPServer> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_tools = new Dictionary<string, Func<JObject, Task<object>>>();
			RegisterTools();
		}

		private void RegisterTools()
		{
			_tools["analyze_source_code"] = HandleAnalyzeSourceCode;
			_tools["generate_migration_structure"] = HandleGenerateMigrationStructure;
			_tools["prepare_migration_chunks"] = HandlePrepareMigrationChunks;
			_tools["get_next_chunk"] = HandleGetNextChunk;
			_tools["process_migration_chunk"] = HandleProcessMigrationChunk;
			_tools["save_migrated_code"] = HandleSaveMigratedCode;
			_tools["get_migration_status"] = HandleGetMigrationStatus;
		}

		public async Task<string> ProcessRequest(string jsonRequest)
		{
			try
			{
				var request = JObject.Parse(jsonRequest);
				var method = request["method"]?.ToString();
				var id = request["id"];

				var safeId = id ?? 0;

				if (method == "initialize")
				{
					return JsonConvert.SerializeObject(new
					{
						jsonrpc = "2.0",
						id = safeId,
						result = new
						{
							protocolVersion = "2024-11-05",
							capabilities = new
							{
								tools = new { }
							},
							serverInfo = new
							{
								name = "webform-migration",
								version = "1.0.0"
							}
						}
					}, Formatting.None);
				}

				switch (method)
				{
					case "tools/list":
						return JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = safeId,
							result = GetToolsList()
						}, Formatting.None);

					case "tools/call":
						var toolName = request["params"]?["name"]?.ToString();
						var arguments = request["params"]?["arguments"] as JObject ?? new JObject();

						if (_tools.ContainsKey(toolName))
						{
							try
							{
								var result = await _tools[toolName](arguments);
								return JsonConvert.SerializeObject(new
								{
									jsonrpc = "2.0",
									id = safeId,
									result = new
									{
										content = new object[]
										{
											new
											{
												type = "text",
												text = JsonConvert.SerializeObject(result, Formatting.Indented)
											}
										}
									}
								}, Formatting.None);
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, $"Error executing tool {toolName}");
								return JsonConvert.SerializeObject(new
								{
									jsonrpc = "2.0",
									id = safeId,
									error = new
									{
										code = -32603,
										message = "Tool execution failed",
										data = ex.Message
									}
								}, Formatting.None);
							}
						}
						else
						{
							return JsonConvert.SerializeObject(new
							{
								jsonrpc = "2.0",
								id = safeId,
								error = new
								{
									code = -32601,
									message = $"Tool not found: {toolName}"
								}
							}, Formatting.None);
						}

					case "resources/list":
						return JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = safeId,
							result = new
							{
								resources = new object[] { }
							}
						}, Formatting.None);

					case "prompts/list":
						return JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = safeId,
							result = new
							{
								prompts = new object[] { }
							}
						}, Formatting.None);

					default:
						return JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = safeId,
							error = new
							{
								code = -32601,
								message = $"Method not found: {method}"
							}
						}, Formatting.None);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing MCP request");
				return JsonConvert.SerializeObject(new
				{
					jsonrpc = "2.0",
					id = 0,
					error = new
					{
						code = -32603,
						message = "Internal error",
						data = ex.Message
					}
				}, Formatting.None);
			}
		}

		private object GetToolsList()
		{
			return new
			{
				tools = new object[]
				{
					new
					{
						name = "analyze_source_code",
						description = "Analyze C# source code directory using Roslyn metrics",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["directory_path"] = new { type = "string", description = "Path to source code directory" },
								["include_subdirectories"] = new { type = "boolean", description = "Include subdirectories in analysis" }
							},
							required = new string[] { "directory_path" }
						}
					},
					new
					{
						name = "generate_migration_structure",
						description = "Generate migration structure based on analyzed code",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["analysis_id"] = new { type = "string", description = "Analysis ID from previous step" }
							},
							required = new string[] { "analysis_id" }
						}
					},
					new
					{
						name = "prepare_migration_chunks",
						description = "Prepare code chunks for migration",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["analysis_id"] = new { type = "string", description = "Analysis ID" },
								["max_tokens_percentage"] = new { type = "number", description = "Max percentage of token limit to use (default: 0.7)" }
							},
							required = new string[] { "analysis_id" }
						}
					},
					new
					{
						name = "get_next_chunk",
						description = "Get the next unprocessed code chunk for migration",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["analysis_id"] = new { type = "string", description = "Analysis ID" }
							},
							required = new string[] { "analysis_id" }
						}
					},
					new
					{
						name = "process_migration_chunk",
						description = "Mark a chunk as processed and save the migrated code",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["chunk_id"] = new { type = "string", description = "Code chunk ID" },
								["migrated_code"] = new { type = "string", description = "The migrated code from Claude Desktop" }
							},
							required = new string[] { "chunk_id", "migrated_code" }
						}
					},
					new
					{
						name = "save_migrated_code",
						description = "Save migrated code to output directory",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["analysis_id"] = new { type = "string", description = "Analysis ID" },
								["output_directory"] = new { type = "string", description = "Output directory path" }
							},
							required = new string[] { "analysis_id", "output_directory" }
						}
					},
					new
					{
						name = "get_migration_status",
						description = "Get current migration status",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["analysis_id"] = new { type = "string", description = "Analysis ID" }
							},
							required = new string[] { "analysis_id" }
						}
					}
				}
			};
		}

		private async Task<object> HandleAnalyzeSourceCode(JObject args)
		{
			try
			{
				var directoryPath = args["directory_path"]?.ToString();
				var includeSubdirectories = args["include_subdirectories"]?.ToObject<bool>() ?? true;

				if (string.IsNullOrEmpty(directoryPath))
				{
					throw new ArgumentException("directory_path is required");
				}

				var analyzer = _serviceProvider.GetRequiredService<ISourceCodeAnalyzer>();
				var analysisResult = await analyzer.AnalyzeDirectoryAsync(directoryPath, includeSubdirectories);

				return new
				{
					analysis_id = analysisResult.Id,
					summary = new
					{
						total_files = analysisResult.TotalFiles,
						total_classes = analysisResult.TotalClasses,
						total_methods = analysisResult.TotalMethods,
						complexity_score = analysisResult.ComplexityScore
					},
					message = "Source code analysis completed successfully"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleAnalyzeSourceCode");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandleGenerateMigrationStructure(JObject args)
		{
			try
			{
				var analysisId = args["analysis_id"]?.ToString();

				if (string.IsNullOrEmpty(analysisId))
				{
					throw new ArgumentException("analysis_id is required");
				}

				var structureGenerator = _serviceProvider.GetRequiredService<IMigrationStructureGenerator>();
				var structure = await structureGenerator.GenerateStructureAsync(analysisId);

				return new
				{
					structure_generated = true,
					business_logic_interfaces = structure.BusinessLogicInterfaces.Count,
					data_access_interfaces = structure.DataAccessInterfaces.Count,
					models = structure.Models.Count,
					message = "Migration structure generated successfully"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleGenerateMigrationStructure");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandlePrepareMigrationChunks(JObject args)
		{
			try
			{
				var analysisId = args["analysis_id"]?.ToString();
				var maxTokensPercentage = args["max_tokens_percentage"]?.ToObject<double>() ?? 0.7;

				if (string.IsNullOrEmpty(analysisId))
				{
					throw new ArgumentException("analysis_id is required");
				}

				var migrator = _serviceProvider.GetRequiredService<ICodeMigrator>();
				var result = await migrator.MigrateCodeChunksAsync(analysisId, maxTokensPercentage);

				return new
				{
					chunks_prepared = true,
					total_chunks = result.ChunksProcessed + result.ChunksRemaining,
					chunks_remaining = result.ChunksRemaining,
					message = result.ChunksRemaining > 0 ?
						$"Prepared {result.ChunksRemaining} chunks for migration. Use 'get_next_chunk' to start processing." :
						"All chunks already processed."
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandlePrepareMigrationChunks");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandleGetNextChunk(JObject args)
		{
			try
			{
				var analysisId = args["analysis_id"]?.ToString();

				if (string.IsNullOrEmpty(analysisId))
				{
					throw new ArgumentException("analysis_id is required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var codeChunks = await repository.GetCodeChunksAsync(analysisId);
				var nextChunk = codeChunks.FirstOrDefault(c => !c.IsProcessed);

				if (nextChunk == null)
				{
					return new
					{
						has_next_chunk = false,
						message = "No more chunks to process. All migration chunks completed!"
					};
				}

				var migrationStructure = await repository.GetMigrationStructureAsync(analysisId);
				var context = BuildMigrationContext(migrationStructure);

				return new
				{
					has_next_chunk = true,
					chunk_id = nextChunk.Id,
					estimated_tokens = nextChunk.EstimatedTokens,
					context = context,
					code_to_migrate = nextChunk.CombinedCode,
					migration_prompt = BuildMigrationPrompt(nextChunk.CombinedCode, context),
					progress = new
					{
						total_chunks = codeChunks.Count,
						completed_chunks = codeChunks.Count(c => c.IsProcessed),
						remaining_chunks = codeChunks.Count(c => !c.IsProcessed)
					}
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleGetNextChunk");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandleProcessMigrationChunk(JObject args)
		{
			try
			{
				var chunkId = args["chunk_id"]?.ToString();
				var migratedCode = args["migrated_code"]?.ToString();

				if (string.IsNullOrEmpty(chunkId) || string.IsNullOrEmpty(migratedCode))
				{
					throw new ArgumentException("chunk_id and migrated_code are required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var statusService = _serviceProvider.GetRequiredService<IMigrationStatusService>();

				var allChunks = await repository.GetCodeChunksAsync("");
				var chunk = allChunks.FirstOrDefault(c => c.Id == chunkId);

				if (chunk == null)
				{
					return new { error = true, message = "Chunk not found" };
				}

				chunk.MigratedCode = migratedCode;
				chunk.IsProcessed = true;
				await repository.UpdateCodeChunkAsync(chunk);

				var allAnalysisChunks = await repository.GetCodeChunksAsync(chunk.AnalysisId);
				var completed = allAnalysisChunks.Count(c => c.IsProcessed);
				var total = allAnalysisChunks.Count;

				await statusService.UpdateStatusAsync(chunk.AnalysisId, "Processing Migration", completed, total);

				return new
				{
					success = true,
					chunk_processed = true,
					progress = new
					{
						total_chunks = total,
						completed_chunks = completed,
						remaining_chunks = total - completed,
						progress_percentage = total > 0 ? (double)completed / total * 100 : 100
					},
					message = total - completed > 0 ?
						$"Chunk processed successfully. {total - completed} chunks remaining." :
						"All chunks completed! You can now save the migrated code."
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleProcessMigrationChunk");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandleSaveMigratedCode(JObject args)
		{
			try
			{
				var analysisId = args["analysis_id"]?.ToString();
				var outputDirectory = args["output_directory"]?.ToString();

				if (string.IsNullOrEmpty(analysisId) || string.IsNullOrEmpty(outputDirectory))
				{
					throw new ArgumentException("analysis_id and output_directory are required");
				}

				var codeSaver = _serviceProvider.GetRequiredService<IMigratedCodeSaver>();
				var result = await codeSaver.SaveMigratedCodeAsync(analysisId, outputDirectory);

				return new
				{
					files_saved = result.FilesSaved,
					output_directory = result.OutputDirectory,
					web_form_generated = result.WebFormGenerated,
					message = "Migrated code saved successfully"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleSaveMigratedCode");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private async Task<object> HandleGetMigrationStatus(JObject args)
		{
			try
			{
				var analysisId = args["analysis_id"]?.ToString();

				if (string.IsNullOrEmpty(analysisId))
				{
					throw new ArgumentException("analysis_id is required");
				}

				var statusService = _serviceProvider.GetRequiredService<IMigrationStatusService>();
				var status = await statusService.GetStatusAsync(analysisId);

				return new
				{
					analysis_id = analysisId,
					total_items = status.TotalItems,
					migrated_items = status.MigratedItems,
					pending_items = status.PendingItems,
					failed_items = status.FailedItems,
					progress_percentage = status.ProgressPercentage,
					current_phase = status.CurrentPhase,
					errors = status.Errors
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in HandleGetMigrationStatus");
				return new
				{
					error = true,
					message = ex.Message
				};
			}
		}

		private string BuildMigrationContext(MigrationStructure migrationStructure)
		{
			if (migrationStructure == null)
				return string.Empty;

			var sb = new StringBuilder();

			sb.AppendLine("MIGRATION CONTEXT:");
			sb.AppendLine("You are helping migrate legacy .NET 4.8 WebForms code to a modern layered architecture.");
			sb.AppendLine();

			sb.AppendLine("TARGET ARCHITECTURE:");
			sb.AppendLine("- Separate Business Logic from Data Access");
			sb.AppendLine("- Use async/await patterns");
			sb.AppendLine("- Follow dependency injection principles");
			sb.AppendLine("- Create clean, testable interfaces");
			sb.AppendLine();

			if (migrationStructure.DataAccessInterfaces.Any())
			{
				sb.AppendLine("DATA ACCESS INTERFACES TO IMPLEMENT:");
				foreach (var da in migrationStructure.DataAccessInterfaces.Take(3))
				{
					sb.AppendLine($"- {da.Name} (Purpose: {da.Purpose})");
				}
				if (migrationStructure.DataAccessInterfaces.Count > 3)
				{
					sb.AppendLine($"- ... and {migrationStructure.DataAccessInterfaces.Count - 3} more interfaces");
				}
				sb.AppendLine();
			}

			if (migrationStructure.BusinessLogicInterfaces.Any())
			{
				sb.AppendLine("BUSINESS LOGIC INTERFACES TO IMPLEMENT:");
				foreach (var bl in migrationStructure.BusinessLogicInterfaces.Take(3))
				{
					sb.AppendLine($"- {bl.Name} (Purpose: {bl.Purpose})");
				}
				if (migrationStructure.BusinessLogicInterfaces.Count > 3)
				{
					sb.AppendLine($"- ... and {migrationStructure.BusinessLogicInterfaces.Count - 3} more interfaces");
				}
				sb.AppendLine();
			}

			return sb.ToString();
		}

		private string BuildMigrationPrompt(string codeChunk, string context)
		{
			var sb = new StringBuilder();

			sb.AppendLine(context);
			sb.AppendLine("INSTRUCTIONS:");
			sb.AppendLine("1. Analyze the provided legacy code");
			sb.AppendLine("2. Separate business logic from data access");
			sb.AppendLine("3. Create appropriate interface implementations");
			sb.AppendLine("4. Use async/await patterns where appropriate");
			sb.AppendLine("5. Remove direct database dependencies from business logic");
			sb.AppendLine("6. Follow SOLID principles");
			sb.AppendLine("7. Add proper error handling and logging");
			sb.AppendLine("8. Provide XML documentation for public methods");
			sb.AppendLine();

			sb.AppendLine("LEGACY CODE TO MIGRATE:");
			sb.AppendLine("```csharp");
			sb.AppendLine(codeChunk);
			sb.AppendLine("```");
			sb.AppendLine();

			sb.AppendLine("Please provide the migrated code with:");
			sb.AppendLine("- Separate interface definitions");
			sb.AppendLine("- Implementation classes");
			sb.AppendLine("- Proper dependency injection setup");
			sb.AppendLine("- Clear separation of concerns");
			sb.AppendLine("- Modern C# patterns and best practices");
			sb.AppendLine();
			sb.AppendLine("Format your response as ready-to-use C# code files.");

			return sb.ToString();
		}
	}
}