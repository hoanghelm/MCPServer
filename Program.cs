using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.IO;
using CSharpLegacyMigrationMCP.Data;
using CSharpLegacyMigrationMCP.Models;
using CSharpLegacyMigrationMCP.Services;

namespace CSharpLegacyMigrationMCP
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var originalOut = Console.Out;
			var originalError = Console.Error;

			try
			{
				// Temporarily suppress console output during startup
				Console.SetOut(TextWriter.Null);
				Console.SetError(TextWriter.Null);

				var host = CreateHostBuilder(args).Build();

				using var scope = host.Services.CreateScope();

				// Restore console streams
				Console.SetOut(originalOut);
				Console.SetError(originalError);

				var logger = host.Services.GetRequiredService<ILogger<Program>>();
				logger.LogInformation("VS Code WebForm Migration MCP Server v2.0.0 starting...");

				// Test database connection
				try
				{
					var repository = host.Services.GetRequiredService<IDataRepository>();
					var connectionTest = await repository.TestConnectionAsync();

					if (connectionTest)
					{
						logger.LogInformation("Database connection successful");
						Console.Error.WriteLine("Database connection successful");
					}
					else
					{
						logger.LogWarning("Database connection failed - some features may not work");
						Console.Error.WriteLine("Warning: Database connection failed");
					}
				}
				catch (Exception dbEx)
				{
					logger.LogError(dbEx, "Database initialization error");
					Console.Error.WriteLine($"Database error: {dbEx.Message}");
				}

				Console.Error.WriteLine("VS Code WebForm Migration MCP Server ready...");
				Console.Error.Flush();

				var server = host.Services.GetRequiredService<MCPServer>();

				string line;
				while ((line = await Console.In.ReadLineAsync()) != null)
				{
					try
					{
						var response = await server.ProcessRequest(line);
						Console.WriteLine(response);
						Console.Out.Flush();
					}
					catch (JsonException jsonEx)
					{
						logger.LogError(jsonEx, "JSON parsing error");
						var errorResponse = JsonConvert.SerializeObject(new
						{
							jsonrpc = "2.0",
							id = 0,
							error = new
							{
								code = -32700,
								message = "Parse error",
								data = jsonEx.Message
							}
						}, Formatting.None);

						Console.WriteLine(errorResponse);
						Console.Out.Flush();
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error processing request");
						var errorResponse = JsonConvert.SerializeObject(new
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

						Console.WriteLine(errorResponse);
						Console.Out.Flush();
					}
				}
			}
			catch (Exception ex)
			{
				Console.SetError(originalError);
				Console.Error.WriteLine($"Fatal startup error: {ex.Message}");
				Environment.Exit(1);
			}
		}

		static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((context, config) =>
				{
					config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
					config.AddEnvironmentVariables("WEBFORM_MIGRATION_");
					config.AddCommandLine(args);
				})
				.ConfigureServices((context, services) =>
				{
					// Configure logging
					services.AddLogging(builder =>
					{
						builder.ClearProviders();
						builder.AddFilter("Microsoft", LogLevel.Warning);
						builder.AddFilter("System", LogLevel.Warning);
						builder.SetMinimumLevel(LogLevel.Information);
					});

					// Register core services
					services.AddSingleton<MCPServer>();
					services.AddScoped<IWorkspaceAnalyzer, WorkspaceAnalyzer>();
					services.AddScoped<IProjectCreator, ProjectCreator>();
					services.AddScoped<IFileMigrator, FileMigrator>();
					services.AddScoped<IMigrationOrchestrator, MigrationOrchestrator>();
					services.AddScoped<IDataRepository, PostgreSqlRepository>();
					services.AddScoped<IMigrationPromptBuilder, MigrationPromptBuilder>();
					services.AddScoped<IDependencyAnalyzer, DependencyAnalyzer>();

					// Configure database options
					services.Configure<DatabaseOptions>(options =>
					{
						var connectionString = context.Configuration.GetConnectionString("Default") ??
											   Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") ??
											   "Host=localhost;Database=webform_migration;Username=postgres;Password=postgres;Port=5432";

						options.ConnectionString = connectionString;
						options.Provider = "PostgreSQL";
					});
				});
	}
}