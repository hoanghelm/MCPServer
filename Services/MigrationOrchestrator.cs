using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CSharpLegacyMigrationMCP.Models;

namespace CSharpLegacyMigrationMCP.Services
{
	public class MigrationOrchestrator : IMigrationOrchestrator
	{
		private readonly ILogger<MigrationOrchestrator> _logger;
		private readonly IDataRepository _repository;
		private readonly IProjectCreator _projectCreator;

		public MigrationOrchestrator(
			ILogger<MigrationOrchestrator> logger,
			IDataRepository repository,
			IProjectCreator projectCreator)
		{
			_logger = logger;
			_repository = repository;
			_projectCreator = projectCreator;
		}

		public async Task<MigrationStartResult> StartMigrationAsync(string projectId, bool createProjects)
		{
			try
			{
				_logger.LogInformation($"Starting migration for project {projectId}");

				// Get workspace analysis
				var analysis = await _repository.GetWorkspaceAnalysisAsync(projectId);
				if (analysis == null)
				{
					throw new MigrationException($"Project analysis not found: {projectId}");
				}

				// Check if migration already started
				var existingProject = await _repository.GetMigrationProjectAsync(projectId);
				if (existingProject != null && existingProject.Status != ProjectStatus.Failed)
				{
					return new MigrationStartResult
					{
						Success = true,
						ProjectsCreated = false,
						DalProjectPath = existingProject.DalProjectPath,
						BalProjectPath = existingProject.BalProjectPath,
						TotalFilesToMigrate = analysis.CsFiles.Count(f => f.MigrationStatus == MigrationStatus.Pending)
					};
				}

				// Create projects if requested
				string dalPath = null, balPath = null;
				if (createProjects)
				{
					var (dal, bal) = await _projectCreator.CreateProjectsAsync(
						analysis.WorkspacePath,
						analysis.ProjectName);
					dalPath = dal;
					balPath = bal;
				}

				// Create migration project record
				var migrationProject = new MigrationProject
				{
					ProjectId = projectId,
					ProjectName = analysis.ProjectName,
					WorkspacePath = analysis.WorkspacePath,
					DalProjectPath = dalPath,
					BalProjectPath = balPath,
					Status = ProjectStatus.ReadyForMigration
				};

				await _repository.SaveMigrationProjectAsync(migrationProject);

				// Count files to migrate
				var filesToMigrate = analysis.CsFiles
					.Where(f => f.FileType != FileType.Unknown.ToString() &&
							   f.FileType != FileType.Model.ToString() &&
							   f.MigrationStatus == MigrationStatus.Pending)
					.Count();

				_logger.LogInformation($"Migration initialized. {filesToMigrate} files ready to migrate.");

				return new MigrationStartResult
				{
					Success = true,
					ProjectsCreated = createProjects,
					DalProjectPath = dalPath,
					BalProjectPath = balPath,
					TotalFilesToMigrate = filesToMigrate
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error starting migration for project {projectId}");
				return new MigrationStartResult
				{
					Success = false,
					Error = ex.Message
				};
			}
		}

		public async Task<RetryResult> RetryFailedFilesAsync(string projectId)
		{
			try
			{
				_logger.LogInformation($"Retrying failed files for project {projectId}");

				// Get all failed files
				var files = await _repository.GetProjectFilesAsync(projectId, "failed");

				if (!files.Any())
				{
					return new RetryResult
					{
						FailedFilesCount = 0,
						RetryStarted = false
					};
				}

				// Reset status of failed files to pending
				foreach (var file in files)
				{
					await _repository.UpdateFileStatusAsync(file.Id, MigrationStatus.Pending);
				}

				_logger.LogInformation($"Reset {files.Count} failed files to pending status");

				return new RetryResult
				{
					FailedFilesCount = files.Count,
					RetryStarted = true
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error retrying failed files for project {projectId}");
				throw;
			}
		}
	}
}