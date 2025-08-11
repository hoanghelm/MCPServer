using System;
using System.Configuration;
using System.Web.UI;
using SimpleWebform.Services.BusinessLogics;
using SimpleWebform.Services.Models;
using UserManagement.Helpers;

namespace UserManagement
{
	public partial class Login : Page
	{
		private string connectionString = ConfigurationManager.ConnectionStrings["PostgreSQLConnection"].ConnectionString;

		protected void Page_Load(object sender, EventArgs e)
		{
			if (Session["UserID"] != null)
			{
				Server.Transfer("UserManagement.aspx");
			}
		}

		protected async void btnLogin_Click(object sender, EventArgs e)
		{
			try
			{
				var loginBusiness = ServiceResolver.TryResolve<ILoginBusiness>();

				var loginRequest = new LoginRequest 
				{
					Username = txtUsername.Text.Trim(),
					Password = txtPassword.Text.Trim()
				};

				var result = await loginBusiness.AuthenticateAsync(loginRequest);

				if (result.IsSuccess && result.User != null)
				{
					Session["UserID"] = result.User.UserId;
					Session["Username"] = result.User.Username;
					Session["Email"] = result.User.Email;
					Server.Transfer("UserManagement.aspx");
				}
				else
				{
					lblMessage.Text = result.Message;
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "An error occurred during login.";
				System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
			}
		}
	}
}