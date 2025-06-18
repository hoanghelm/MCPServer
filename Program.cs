using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.IO;
using CSharpLegacyMigrationMCP.Data;
using CSharpLegacyMigrationMCP.Models;
using CSharpLegacyMigrationMCP.Services;
using CSharpLegacyMigrationMCP;

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
				Console.SetOut(TextWriter.Null);
				Console.SetError(TextWriter.Null);

				var host = CreateHostBuilder(args).Build();

				using var scope = host.Services.CreateScope();

				Console.SetOut(originalOut);
				Console.SetError(originalError);

				var logger = host.Services.GetRequiredService<ILogger<Program>>();
				logger.LogInformation("WebForm Migration MCP Server starting...");

				Console.Error.WriteLine("WebForm Migration MCP Server starting...");
				Console.Error.Flush();

				var server = host.Services.GetRequiredService<MCPServer>();

				string line;
				while ((line = await Console.In.ReadLineAsync()) != null)
				{
					try
					{
						Console.Error.WriteLine($"Received: {line}");
						Console.Error.Flush();

						var response = await server.ProcessRequest(line);

						Console.Error.WriteLine($"Sending: {response.Substring(0, Math.Min(200, response.Length))}...");
						Console.Error.Flush();

						Console.WriteLine(response);
						Console.Out.Flush();
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error processing request: {Request}", line);
						Console.Error.WriteLine($"Error processing request: {ex.Message}");
						Console.Error.Flush();

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
				Console.Error.WriteLine($"Stack: {ex.StackTrace}");
				Console.Error.Flush();

				var errorResponse = JsonConvert.SerializeObject(new
				{
					jsonrpc = "2.0",
					id = 0,
					error = new
					{
						code = -32000,
						message = "Server startup failed",
						data = ex.Message
					}
				}, Formatting.None);

				Console.WriteLine(errorResponse);
				Console.Out.Flush();
				Environment.Exit(1);
			}
		}

		static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureServices((context, services) =>
				{
					services.AddLogging(builder =>
					{
						builder.ClearProviders();
						builder.AddFilter("Microsoft", LogLevel.Warning);
						builder.AddFilter("System", LogLevel.Warning);
						builder.SetMinimumLevel(LogLevel.Information);
					});

					services.AddSingleton<MCPServer>();
					services.AddScoped<ISourceCodeAnalyzer, RoslynSourceCodeAnalyzer>();
					services.AddScoped<IMigrationStructureGenerator, MigrationStructureGenerator>();
					services.AddScoped<ICodeMigrator, ClaudeDesktopMigrator>();
					services.AddScoped<IMigratedCodeSaver, MigratedCodeSaver>();
					services.AddScoped<IDataRepository, PostgreSqlRepository>();

					//services.Configure<DatabaseOptions>(
					//	context.Configuration.GetSection("Database"));
					services.Configure<DatabaseOptions>(options => {
						options.ConnectionString = "Host=localhost;Database=webform_migration;Username=postgres;Password=postgres;Port=5432";
						options.Provider = "PostgreSQL";
					});

					services.AddScoped<IMigrationStatusService>(provider =>
					{
						var logger = provider.GetRequiredService<ILogger<MigrationStatusService>>();
						var repository = provider.GetRequiredService<IDataRepository>();
						var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();
						return new MigrationStatusService(logger, repository, dbOptions.Value.ConnectionString);
					});
				});
	}
}