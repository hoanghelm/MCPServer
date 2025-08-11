using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using CSharpLegacyMigrationMCP.Models;
using CSharpLegacyMigrationMCP.Services;

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
			_tools["analyze_workspace"] = HandleAnalyzeWorkspace;
			_tools["start_migration"] = HandleStartMigration;
			_tools["get_migration_status"] = HandleGetMigrationStatus;
			_tools["get_next_migration_prompt"] = HandleGetNextMigrationPrompt;
			_tools["complete_file_migration"] = HandleCompleteFileMigration;
			_tools["get_file_list"] = HandleGetFileList;
			_tools["retry_failed_files"] = HandleRetryFailedFiles;
			_tools["get_project_structure"] = HandleGetProjectStructure;
		}

		public async Task<string> ProcessRequest(string jsonRequest)
		{
			try
			{
				var request = JObject.Parse(jsonRequest);
				var method = request["method"]?.ToString();
				var id = request["id"] ?? 0;

				if (method == "initialize")
				{
					return JsonConvert.SerializeObject(new
					{
						jsonrpc = "2.0",
						id = id,
						result = new
						{
							protocolVersion = "2024-11-05",
							capabilities = new
							{
								tools = new { }
							},
							serverInfo = new
							{
								name = "vscode-webform-migration",
								version = "2.0.0",
								description = "VS Code WebForm to DAL/BAL Migration Tool with Direct File Writing"
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
							id = id,
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
									id = id,
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
									id = id,
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
								id = id,
								error = new
								{
									code = -32601,
									message = $"Tool not found: {toolName}"
								}
							}, Formatting.None);
						}

					default:
						return JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = id,
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
						name = "analyze_workspace",
						description = "Analyze the current VS Code workspace or specified directory",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["workspace_path"] = new { type = "string", description = "Path to workspace folder (defaults to current workspace)" }
							},
							required = new string[] { }
						}
					},
					new
					{
						name = "start_migration",
						description = "Start the migration process for an analyzed project",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID from analysis" },
								["create_projects"] = new { type = "boolean", description = "Auto-create DAL/BAL projects (default: true)" }
							},
							required = new string[] { "project_id" }
						}
					},
					new
					{
						name = "get_migration_status",
						description = "Get current migration status and progress",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" }
							},
							required = new string[] { "project_id" }
						}
					},
					new
					{
						name = "get_next_migration_prompt",
						description = "Get the next file to migrate with complete prompt for AI to process and write files directly",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" }
							},
							required = new string[] { "project_id" }
						}
					},
					new
					{
						name = "complete_file_migration",
						description = "Mark a file migration as completed after AI has written the files directly",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" },
								["file_id"] = new { type = "string", description = "File ID that was migrated" },
								["migration_notes"] = new { type = "string", description = "Optional notes about the migration" }
							},
							required = new string[] { "project_id", "file_id" }
						}
					},
					new
					{
						name = "get_file_list",
						description = "Get list of all files with their migration status",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" },
								["status_filter"] = new { type = "string", description = "Filter by status: all, migrated, pending, failed" }
							},
							required = new string[] { "project_id" }
						}
					},
					new
					{
						name = "retry_failed_files",
						description = "Retry migration for failed files",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" }
							},
							required = new string[] { "project_id" }
						}
					},
					new
					{
						name = "get_project_structure",
						description = "Get current project structure and existing files for reference",
						inputSchema = new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								["project_id"] = new { type = "string", description = "Project ID" }
							},
							required = new string[] { "project_id" }
						}
					}
				}
			};
		}

		private async Task<object> HandleAnalyzeWorkspace(JObject args)
		{
			try
			{
				var workspacePath = args["workspace_path"]?.ToString();

				if (string.IsNullOrEmpty(workspacePath))
				{
					workspacePath = Environment.CurrentDirectory;
				}

				_logger.LogInformation($"Analyzing workspace: {workspacePath}");

				var analyzer = _serviceProvider.GetRequiredService<IWorkspaceAnalyzer>();
				var analysis = await analyzer.AnalyzeWorkspaceAsync(workspacePath);

				return new
				{
					success = true,
					project_id = analysis.ProjectId,
					project_name = analysis.ProjectName,
					workspace_path = analysis.WorkspacePath,
					summary = new
					{
						total_files = analysis.TotalFiles,
						cs_files = analysis.CsFiles.Count,
						aspx_files = analysis.AspxFiles.Count,
						ascx_files = analysis.AscxFiles.Count,
						web_forms = analysis.WebForms.Count,
						data_access_files = analysis.DataAccessFiles.Count,
						business_logic_files = analysis.BusinessLogicFiles.Count
					},
					file_breakdown = new
					{
						by_type = analysis.FilesByType,
						by_complexity = analysis.FilesByComplexity
					},
					next_step = "Use 'start_migration' with the project_id to begin migration"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error analyzing workspace");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleStartMigration(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();
				var createProjects = args["create_projects"]?.ToObject<bool>() ?? true;

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var orchestrator = _serviceProvider.GetRequiredService<IMigrationOrchestrator>();
				var result = await orchestrator.StartMigrationAsync(projectId, createProjects);

				return new
				{
					success = result.Success,
					project_id = projectId,
					projects_created = result.ProjectsCreated,
					dal_project_path = result.DalProjectPath,
					bal_project_path = result.BalProjectPath,
					total_files_to_migrate = result.TotalFilesToMigrate,
					status = "Migration started",
					next_step = "Use 'get_next_migration_prompt' to get files to migrate with AI prompts",
					workflow = new
					{
						step1 = "Call 'get_next_migration_prompt' to get the next file and its migration prompt",
						step2 = "Process the prompt with AI to write files directly to the target projects",
						step3 = "Call 'complete_file_migration' to mark the file as completed",
						step4 = "Repeat until all files are migrated"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error starting migration");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleGetNextMigrationPrompt(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var migrator = _serviceProvider.GetRequiredService<IFileMigrator>();

				// Get next unmigrated file
				var file = await repository.GetNextUnmigratedFileAsync(projectId);

				if (file == null)
				{
					// No more files to migrate
					var status = await repository.GetMigrationStatusAsync(projectId);
					return new
					{
						success = true,
						migration_complete = true,
						message = "All files have been migrated!",
						total_migrated = status.MigratedFiles,
						total_failed = status.FailedFiles
					};
				}

				// Get project info
				var project = await repository.GetMigrationProjectAsync(projectId);

				// Get the migration result with prompt
				var result = await migrator.MigrateFileAsync(file, project);

				if (!result.Success)
				{
					return new
					{
						success = false,
						error = result.Error
					};
				}

				return new
				{
					success = true,
					file_info = new
					{
						file_id = file.Id,
						file_path = file.FilePath,
						file_name = file.FileName,
						file_type = file.FileType,
						complexity = file.Complexity
					},
					project_info = new
					{
						dal_project_path = project.DalProjectPath,
						bal_project_path = project.BalProjectPath,
						project_name = project.ProjectName
					},
					migration_prompt = result.MigrationPrompt,
					instructions = new
					{
						message = "Process the migration_prompt with AI. The AI should write files directly to the specified project paths.",
						next_step = "After AI completes file writing, call 'complete_file_migration' with the file_id to mark as completed",
						ai_instructions = "The AI has direct file system access and should create files immediately at the specified DAL/BAL project paths."
					}
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting next migration prompt");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleCompleteFileMigration(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();
				var fileId = args["file_id"]?.ToString();
				var migrationNotes = args["migration_notes"]?.ToString();

				if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(fileId))
				{
					throw new ArgumentException("project_id and file_id are required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var migrator = _serviceProvider.GetRequiredService<IFileMigrator>();

				// Get the file and project
				var files = await repository.GetProjectFilesAsync(projectId);
				var file = files.FirstOrDefault(f => f.Id == fileId);

				if (file == null)
				{
					throw new ArgumentException($"File not found: {fileId}");
				}

				var project = await repository.GetMigrationProjectAsync(projectId);

				// Process completion (this will verify created files)
				var result = await migrator.ProcessAiResponseAsync(file, project, migrationNotes ?? "Migration completed");

				return new
				{
					success = result.Success,
					file_completed = result.Success,
					file_info = new
					{
						file_path = result.CurrentFile?.FilePath,
						file_name = result.CurrentFile?.FileName,
						file_type = result.CurrentFile?.FileType
					},
					migration_result = result.Success ? new
					{
						dal_files_created = result.DalFilesCreated,
						bal_files_created = result.BalFilesCreated,
						interfaces_created = result.InterfacesCreated
					} : null,
					error = result.Error,
					progress = new
					{
						files_migrated = result.FilesMigrated,
						files_remaining = result.FilesRemaining,
						progress_percentage = result.ProgressPercentage
					},
					has_more_files = result.HasMoreFiles,
					next_action = result.HasMoreFiles ? "Call 'get_next_migration_prompt' for the next file" : "Migration complete!"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error completing file migration");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleGetMigrationStatus(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var status = await repository.GetMigrationStatusAsync(projectId);

				return new
				{
					success = true,
					project_id = projectId,
					status = status.CurrentStatus,
					progress = new
					{
						total_files = status.TotalFiles,
						migrated_files = status.MigratedFiles,
						failed_files = status.FailedFiles,
						pending_files = status.PendingFiles,
						progress_percentage = status.ProgressPercentage
					},
					projects = new
					{
						dal_project = status.DalProjectPath,
						bal_project = status.BalProjectPath
					},
					started_at = status.StartedAt,
					last_updated = status.LastUpdated,
					estimated_completion = status.EstimatedCompletion
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting migration status");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleGetFileList(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();
				var statusFilter = args["status_filter"]?.ToString() ?? "all";

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var files = await repository.GetProjectFilesAsync(projectId, statusFilter);

				return new
				{
					success = true,
					project_id = projectId,
					filter = statusFilter,
					total_files = files.Count,
					files = files.Select(f => new
					{
						file_id = f.Id,
						file_path = f.FilePath,
						file_name = f.FileName,
						file_type = f.FileType,
						status = f.MigrationStatus.ToString(),
						migrated_at = f.MigratedAt,
						dal_output_files = f.DalOutputPath?.Split(';').Where(s => !string.IsNullOrEmpty(s)),
						bal_output_files = f.BalOutputPath?.Split(';').Where(s => !string.IsNullOrEmpty(s)),
						error = f.ErrorMessage,
						complexity = f.Complexity
					})
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting file list");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleRetryFailedFiles(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var orchestrator = _serviceProvider.GetRequiredService<IMigrationOrchestrator>();
				var result = await orchestrator.RetryFailedFilesAsync(projectId);

				return new
				{
					success = true,
					failed_files_found = result.FailedFilesCount,
					retry_started = result.RetryStarted,
					message = result.RetryStarted ?
						$"Started retry for {result.FailedFilesCount} failed files. Use 'get_next_migration_prompt' to continue." :
						"No failed files found to retry."
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrying failed files");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private async Task<object> HandleGetProjectStructure(JObject args)
		{
			try
			{
				var projectId = args["project_id"]?.ToString();

				if (string.IsNullOrEmpty(projectId))
				{
					throw new ArgumentException("project_id is required");
				}

				var repository = _serviceProvider.GetRequiredService<IDataRepository>();
				var project = await repository.GetMigrationProjectAsync(projectId);

				if (project == null)
				{
					throw new ArgumentException($"Project not found: {projectId}");
				}

				var projectStructure = new
				{
					project_info = new
					{
						project_id = project.ProjectId,
						project_name = project.ProjectName,
						workspace_path = project.WorkspacePath,
						dal_project_path = project.DalProjectPath,
						bal_project_path = project.BalProjectPath,
						status = project.Status.ToString()
					},
					dal_structure = GetDirectoryStructure(project.DalProjectPath),
					bal_structure = GetDirectoryStructure(project.BalProjectPath),
					existing_files = new
					{
						dal_files = GetExistingFiles(project.DalProjectPath),
						bal_files = GetExistingFiles(project.BalProjectPath)
					}
				};

				return new
				{
					success = true,
					project_structure = projectStructure
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting project structure");
				return new
				{
					success = false,
					error = ex.Message
				};
			}
		}

		private object GetDirectoryStructure(string projectPath)
		{
			if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
			{
				return new { exists = false, directories = new string[0] };
			}

			try
			{
				var directories = Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories)
					.Where(d => !d.Contains("bin") && !d.Contains("obj"))
					.Select(d => Path.GetRelativePath(projectPath, d))
					.ToArray();

				return new
				{
					exists = true,
					base_path = projectPath,
					directories = directories
				};
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, $"Error scanning directory structure: {projectPath}");
				return new { exists = false, error = ex.Message };
			}
		}

		private object GetExistingFiles(string projectPath)
		{
			if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
			{
				return new { exists = false, files = new string[0] };
			}

			try
			{
				var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
					.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
							   !f.Contains("/bin/") && !f.Contains("/obj/"))
					.Select(f => new
					{
						relative_path = Path.GetRelativePath(projectPath, f),
						file_name = Path.GetFileName(f),
						last_modified = File.GetLastWriteTime(f),
						size_bytes = new FileInfo(f).Length
					})
					.ToArray();

				return new
				{
					exists = true,
					file_count = files.Length,
					files = files
				};
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, $"Error scanning existing files: {projectPath}");
				return new { exists = false, error = ex.Message };
			}
		}
	}
}