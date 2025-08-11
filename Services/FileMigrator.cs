using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Services
{
	public class FileMigrator : IFileMigrator
	{
		private readonly ILogger<FileMigrator> _logger;
		private readonly IDataRepository _repository;
		private readonly IMigrationPromptBuilder _promptBuilder;
		private readonly IDependencyAnalyzer _dependencyAnalyzer;

		public FileMigrator(
			ILogger<FileMigrator> logger,
			IDataRepository repository,
			IMigrationPromptBuilder promptBuilder,
			IDependencyAnalyzer dependencyAnalyzer)
		{
			_logger = logger;
			_repository = repository;
			_promptBuilder = promptBuilder;
			_dependencyAnalyzer = dependencyAnalyzer;
		}

		public async Task<FileMigrationResult> MigrateNextFileAsync(string projectId)
		{
			try
			{
				// Get next unmigrated file
				var file = await _repository.GetNextUnmigratedFileAsync(projectId);

				if (file == null)
				{
					// No more files to migrate
					var status = await _repository.GetMigrationStatusAsync(projectId);
					return new FileMigrationResult
					{
						Success = true,
						HasMoreFiles = false,
						TotalMigrated = status.MigratedFiles,
						TotalFailed = status.FailedFiles
					};
				}

				// Get project info
				var project = await _repository.GetMigrationProjectAsync(projectId);

				// Build enhanced migration prompt with dependency analysis
				var prompt = await BuildEnhancedMigrationPromptAsync(file, project);

				// Return the prompt for AI to process directly
				return new FileMigrationResult
				{
					Success = true,
					CurrentFile = file,
					MigrationPrompt = prompt,
					RequiresAiProcessing = true,
					HasMoreFiles = true
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error preparing migration for project {projectId}");
				return new FileMigrationResult
				{
					Success = false,
					Error = ex.Message
				};
			}
		}

		public async Task<FileMigrationResult> MigrateFileAsync(ProjectFile file, MigrationProject project)
		{
			// This method now just builds the prompt - AI will handle file creation directly
			try
			{
				_logger.LogInformation($"Preparing migration prompt for file: {file.FileName}");

				// Update status to in progress
				await _repository.UpdateFileStatusAsync(file.Id, MigrationStatus.InProgress);

				// Build enhanced migration prompt with dependency analysis
				var prompt = await BuildEnhancedMigrationPromptAsync(file, project);

				return new FileMigrationResult
				{
					Success = true,
					CurrentFile = file,
					MigrationPrompt = prompt,
					RequiresAiProcessing = true,
					HasMoreFiles = true
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error preparing migration prompt for file: {file.FileName}");
				await _repository.UpdateFileStatusAsync(file.Id, MigrationStatus.Failed, ex.Message);

				return new FileMigrationResult
				{
					Success = false,
					Error = ex.Message,
					CurrentFile = file
				};
			}
		}

		public async Task<FileMigrationResult> ProcessAiResponseAsync(ProjectFile file, MigrationProject project, string aiResponse)
		{
			try
			{
				_logger.LogInformation($"Processing AI completion for file: {file.FileName}");

				// Since AI writes files directly, we just need to:
				// 1. Verify files were created
				// 2. Update file status
				// 3. Track what was created

				var createdFiles = await VerifyAndTrackCreatedFilesAsync(file, project);

				if (!createdFiles.Any())
				{
					throw new MigrationException($"No files were created by AI for {file.FileName}. Please check if the AI has file write permissions and the target directories exist.");
				}

				// Update file status to completed
				file.MigrationStatus = MigrationStatus.Completed;
				file.MigratedAt = DateTime.UtcNow;
				file.MigrationNotes = aiResponse; // Store AI's notes about the migration

				// Store the paths of created files instead of the code content
				file.DalOutputPath = string.Join(";", createdFiles.Where(f => f.StartsWith("DAL")).Select(f => f.Substring(4)));
				file.BalOutputPath = string.Join(";", createdFiles.Where(f => f.StartsWith("BAL")).Select(f => f.Substring(4)));

				await _repository.SaveMigratedFileAsync(file);

				// Update overall migration progress
				await _repository.UpdateMigrationProgressAsync(project.ProjectId);

				// Get updated status for progress tracking
				var status = await _repository.GetMigrationStatusAsync(project.ProjectId);

				var result = new FileMigrationResult
				{
					Success = true,
					CurrentFile = file,
					FilesMigrated = status.MigratedFiles,
					FilesRemaining = status.PendingFiles,
					ProgressPercentage = status.ProgressPercentage,
					HasMoreFiles = status.PendingFiles > 0,
					DalFilesCreated = createdFiles.Where(f => f.StartsWith("DAL")).Select(f => f.Substring(4)).ToList(),
					BalFilesCreated = createdFiles.Where(f => f.StartsWith("BAL")).Select(f => f.Substring(4)).ToList()
				};

				// Extract interface files from created files
				result.InterfacesCreated = createdFiles
					.Where(f => f.Contains("Interfaces/"))
					.Select(f => Path.GetFileName(f))
					.ToList();

				_logger.LogInformation($"Successfully processed AI response for file: {file.FileName}");
				_logger.LogInformation($"Tracked {result.DalFilesCreated.Count} DAL files and {result.BalFilesCreated.Count} BAL files");

				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error processing AI response for file: {file.FileName}");

				// Update file status to failed
				await _repository.UpdateFileStatusAsync(file.Id, MigrationStatus.Failed, ex.Message);

				// Get updated status even for failed case
				var status = await _repository.GetMigrationStatusAsync(project.ProjectId);

				return new FileMigrationResult
				{
					Success = false,
					Error = ex.Message,
					CurrentFile = file,
					FilesMigrated = status.MigratedFiles,
					FilesRemaining = status.PendingFiles,
					ProgressPercentage = status.ProgressPercentage,
					HasMoreFiles = status.PendingFiles > 0
				};
			}
		}

		private async Task<string> BuildEnhancedMigrationPromptAsync(ProjectFile file, MigrationProject project)
		{
			try
			{
				_logger.LogInformation($"Building enhanced migration prompt with dependency analysis for: {file.FileName}");

				// Use the enhanced prompt builder with dependency analysis
				var prompt = await _promptBuilder.BuildMigrationPromptAsync(file, project.ProjectName, project.ProjectId, project);

				// Log related files found for debugging
				var relatedFiles = await _dependencyAnalyzer.GetRelatedMigratedFilesAsync(file, project.ProjectId);
				if (relatedFiles.Any())
				{
					_logger.LogInformation($"Found {relatedFiles.Count} related migrated files for {file.FileName}: {string.Join(", ", relatedFiles.Select(f => f.FileName))}");
				}
				else
				{
					_logger.LogInformation($"No related migrated files found for {file.FileName}");
				}

				return prompt;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error building enhanced migration prompt for {file.FileName}");

				// Fallback to basic prompt without dependencies
				_logger.LogWarning($"Falling back to basic prompt for {file.FileName}");
				return _promptBuilder.BuildMigrationPrompt(file, project.ProjectName);
			}
		}

		private List<string> GetExistingProjectFiles(string projectPath)
		{
			var files = new List<string>();

			if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
				return files;

			try
			{
				// Get all .cs files in the project, excluding bin and obj folders
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

		private async Task<List<string>> VerifyAndTrackCreatedFilesAsync(ProjectFile file, MigrationProject project)
		{
			var createdFiles = new List<string>();

			try
			{
				var cutoffTime = DateTime.Now.AddMinutes(-10); // Files modified in last 10 minutes

				// Check DAL project for new files
				if (!string.IsNullOrEmpty(project.DalProjectPath) && Directory.Exists(project.DalProjectPath))
				{
					var dalFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => File.GetLastWriteTime(f) >= cutoffTime) // Files modified recently
						.Select(f => $"DAL/{Path.GetRelativePath(project.DalProjectPath, f)}")
						.ToList();

					createdFiles.AddRange(dalFiles);

					if (dalFiles.Any())
					{
						_logger.LogInformation($"Detected {dalFiles.Count} DAL files for {file.FileName}: {string.Join(", ", dalFiles)}");
					}
				}

				// Check BAL project for new files
				if (!string.IsNullOrEmpty(project.BalProjectPath) && Directory.Exists(project.BalProjectPath))
				{
					var balFiles = Directory.GetFiles(project.BalProjectPath, "*.cs", SearchOption.AllDirectories)
						.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
								   !f.Contains("/bin/") && !f.Contains("/obj/"))
						.Where(f => File.GetLastWriteTime(f) >= cutoffTime) // Files modified recently
						.Select(f => $"BAL/{Path.GetRelativePath(project.BalProjectPath, f)}")
						.ToList();

					createdFiles.AddRange(balFiles);

					if (balFiles.Any())
					{
						_logger.LogInformation($"Detected {balFiles.Count} BAL files for {file.FileName}: {string.Join(", ", balFiles)}");
					}
				}

				_logger.LogInformation($"Total detected {createdFiles.Count} recently modified files for {file.FileName}");

				// If no files found, try with a longer time window
				if (!createdFiles.Any())
				{
					_logger.LogWarning($"No recently created files found for {file.FileName}, trying with longer time window...");

					var extendedCutoffTime = DateTime.Now.AddMinutes(-30); // Extended to 30 minutes

					// Retry with extended time window
					if (!string.IsNullOrEmpty(project.DalProjectPath) && Directory.Exists(project.DalProjectPath))
					{
						var dalFiles = Directory.GetFiles(project.DalProjectPath, "*.cs", SearchOption.AllDirectories)
							.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
									   !f.Contains("/bin/") && !f.Contains("/obj/"))
							.Where(f => File.GetLastWriteTime(f) >= extendedCutoffTime)
							.Select(f => $"DAL/{Path.GetRelativePath(project.DalProjectPath, f)}")
							.ToList();

						createdFiles.AddRange(dalFiles);
					}

					if (!string.IsNullOrEmpty(project.BalProjectPath) && Directory.Exists(project.BalProjectPath))
					{
						var balFiles = Directory.GetFiles(project.BalProjectPath, "*.cs", SearchOption.AllDirectories)
							.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") &&
									   !f.Contains("/bin/") && !f.Contains("/obj/"))
							.Where(f => File.GetLastWriteTime(f) >= extendedCutoffTime)
							.Select(f => $"BAL/{Path.GetRelativePath(project.BalProjectPath, f)}")
							.ToList();

						createdFiles.AddRange(balFiles);
					}

					if (createdFiles.Any())
					{
						_logger.LogInformation($"Found {createdFiles.Count} files with extended time window for {file.FileName}");
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, $"Error verifying created files for {file.FileName}");
			}

			return createdFiles;
		}

		private bool IsRecentlyCreated(string filePath, DateTime cutoffTime)
		{
			try
			{
				var fileInfo = new FileInfo(filePath);
				return fileInfo.Exists &&
					   (fileInfo.CreationTime >= cutoffTime || fileInfo.LastWriteTime >= cutoffTime);
			}
			catch
			{
				return false;
			}
		}

		private string GetEntityNameFromFile(ProjectFile file)
		{
			// Extract entity name from file name for better tracking
			var fileName = Path.GetFileNameWithoutExtension(file.FileName);

			// Remove common suffixes to get entity name
			var patterns = new[]
			{
				@"(.+)Repository$",
				@"(.+)Service$",
				@"(.+)Manager$",
				@"(.+)Controller$",
				@"(.+)Dal$",
				@"(.+)Business$"
			};

			foreach (var pattern in patterns)
			{
				var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				if (match.Success && match.Groups.Count > 1)
				{
					return match.Groups[1].Value;
				}
			}

			return fileName;
		}
	}
}