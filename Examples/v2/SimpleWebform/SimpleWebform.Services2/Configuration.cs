using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleWebform.Services.BusinessLogics;
using SimpleWebform.Services.DataAccess;
using SimpleWebform.Services.Utils;
using System.Collections.Generic;
using System.Configuration;

namespace SimpleWebform.Services
{
	/// <summary>
	/// Extension methods for dependency injection configuration
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Adds user management services to the dependency injection container
		/// </summary>
		/// <param name="builder">Container builder</param>
		/// <returns>Container builder for chaining</returns>
		public static ContainerBuilder AddUserManagementServices(this ContainerBuilder builder)
		{
			// Register IConfiguration
			var configBuilder = new ConfigurationBuilder();
			var connectionString = "Host=localhost;Database=UserManagement;Username=postgres;Password=postgres;";

			// Create a simple configuration with the connection string
			var configData = new Dictionary<string, string>
			{
				{ "ConnectionStrings:PostgreSQLConnection", connectionString }
			};
			configBuilder.AddInMemoryCollection(configData);
			var configuration = configBuilder.Build();

			builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();

			// Register logging - using NullLogger for simplicity (no actual logging)
			builder.RegisterGeneric(typeof(NullLogger<>)).As(typeof(ILogger<>)).SingleInstance();

			// Register Services first (lowest level dependencies)
			builder.RegisterType<PasswordService>().As<IPasswordService>().InstancePerLifetimeScope();

			// Register Data Access Layer (now has all dependencies)
			builder.RegisterType<UsersDA>().As<IUsersDA>().InstancePerLifetimeScope();

			// Register Business Logic Layer (now has all dependencies)
			builder.RegisterType<LoginBusiness>().As<ILoginBusiness>().InstancePerLifetimeScope();
			builder.RegisterType<UserBusiness>().As<IUserBusiness>().InstancePerLifetimeScope();

			return builder;
		}
	}
}