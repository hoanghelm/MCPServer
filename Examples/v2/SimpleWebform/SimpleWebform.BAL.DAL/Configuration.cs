using Autofac;
using Microsoft.Extensions.DependencyInjection;
using SimpleWebform.Services.BusinessLogics;
using SimpleWebform.Services.DataAccess;
using SimpleWebform.Services.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		/// <param name="services">Service collection</param>
		/// <returns>Service collection for chaining</returns>
		public static ContainerBuilder AddUserManagementServices(this ContainerBuilder builder)
		{
			// Register Data Access Layer
			builder.RegisterType<UsersDA>().As<IUsersDA>().InstancePerLifetimeScope();

			// Register Business Logic Layer
			builder.RegisterType<LoginBusiness>().As<ILoginBusiness>().InstancePerLifetimeScope();
			builder.RegisterType<UserBusiness>().As<IUserBusiness>().InstancePerLifetimeScope();

			// Register Services
			builder.RegisterType<PasswordService>().As<IPasswordService>().InstancePerLifetimeScope();

			return builder;
		}
	}
}
