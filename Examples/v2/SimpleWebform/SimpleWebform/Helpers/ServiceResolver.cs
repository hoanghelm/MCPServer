using System;
using System.Web;
using Autofac;
using Autofac.Integration.Web;

namespace UserManagement.Helpers
{
	/// <summary>
	/// Helper class to resolve services from Autofac container in WebForms
	/// </summary>
	public static class ServiceResolver
	{
		/// <summary>
		/// Resolves a service from the current Autofac container
		/// </summary>
		/// <typeparam name="T">Type of service to resolve</typeparam>
		/// <returns>The resolved service instance</returns>
		public static T Resolve<T>()
		{
			var containerProvider = HttpContext.Current.ApplicationInstance as IContainerProviderAccessor;
			if (containerProvider?.ContainerProvider?.RequestLifetime == null)
			{
				throw new InvalidOperationException("Autofac container not available in current context.");
			}

			return containerProvider.ContainerProvider.RequestLifetime.Resolve<T>();
		}

		/// <summary>
		/// Tries to resolve a service, returns null if not found
		/// </summary>
		/// <typeparam name="T">Type of service to resolve</typeparam>
		/// <returns>The resolved service instance or null</returns>
		public static T TryResolve<T>() where T : class
		{
			try
			{
				return Resolve<T>();
			}
			catch
			{
				return null;
			}
		}
	}
}