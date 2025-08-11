using System;
using System.Configuration;
using System.Web.UI;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace UserManagement
{
	public partial class Login : Page
	{
		private string connectionString = ConfigurationManager.ConnectionStrings["PostgreSQLConnection"].ConnectionString;

		protected void Page_Load(object sender, EventArgs e)
		{
			if (Session["UserID"] != null)
			{
				Response.Redirect("UserManagement.aspx");
			}
		}

		protected void btnLogin_Click(object sender, EventArgs e)
		{
			string username = txtUsername.Text.Trim();
			string password = txtPassword.Text.Trim();

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
			{
				lblMessage.Text = "Please enter username and password.";
				return;
			}

			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();
					string hashedPassword = HashPassword(password);

					string query = "SELECT userid, username, email, isactive FROM users WHERE username = @username AND password = @password AND isactive = true";
					using (var command = new NpgsqlCommand(query, connection))
					{
						command.Parameters.AddWithValue("@username", username);
						command.Parameters.AddWithValue("@password", hashedPassword);

						using (var reader = command.ExecuteReader())
						{
							if (reader.Read())
							{
								Session["UserID"] = reader["userid"];
								Session["Username"] = reader["username"];
								Session["Email"] = reader["email"];
								Response.Redirect("UserManagement.aspx");
							}
							else
							{
								lblMessage.Text = "Invalid username or password.";
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error: " + ex.Message;
			}
		}

		private string HashPassword(string password)
		{
			using (SHA256 sha256Hash = SHA256.Create())
			{
				byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
				StringBuilder builder = new StringBuilder();
				for (int i = 0; i < bytes.Length; i++)
				{
					builder.Append(bytes[i].ToString("x2"));
				}
				return builder.ToString();
			}
		}
	}
}