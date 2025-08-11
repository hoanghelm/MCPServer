using System;
using System.Web;
using Autofac;
using Autofac.Integration.Web;
using SimpleWebform.Services;

namespace Yemek_Tarifleri_Sitesi
{
	public class Global : System.Web.HttpApplication, IContainerProviderAccessor
	{
		private static IContainerProvider _containerProvider;

		public IContainerProvider ContainerProvider
		{
			get { return _containerProvider; }
		}

		protected void Application_Start(object sender, EventArgs e)
		{
			try
			{
				var builder = new ContainerBuilder();

				builder.AddUserManagementServices();

				var container = builder.Build();

				_containerProvider = new ContainerProvider(container);
			}
			catch (Exception ex)
			{
				throw;
			}
		}

		protected void Application_End(object sender, EventArgs e)
		{
			
		}
	}
}