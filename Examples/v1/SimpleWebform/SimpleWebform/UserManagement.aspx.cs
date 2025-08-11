using System;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace UserManagement
{
	public partial class UserManagement : Page
	{
		private string connectionString = ConfigurationManager.ConnectionStrings["PostgreSQLConnection"].ConnectionString;

		protected void Page_Load(object sender, EventArgs e)
		{
			if (Session["UserID"] == null)
			{
				Response.Redirect("Login.aspx");
				return;
			}

			if (!IsPostBack)
			{
				lblWelcome.Text = Session["Username"].ToString();
				LoadUsers();
			}
		}

		protected void btnLogout_Click(object sender, EventArgs e)
		{
			Session.Clear();
			Response.Redirect("Login.aspx");
		}

		protected void btnSave_Click(object sender, EventArgs e)
		{
			string username = txtUsername.Text.Trim();
			string email = txtEmail.Text.Trim();
			string password = txtPassword.Text.Trim();
			bool isActive = chkIsActive.Checked;

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
			{
				lblMessage.Text = "Please fill all fields.";
				return;
			}

			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();

					// Check if username already exists
					string checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username";
					using (var checkCommand = new NpgsqlCommand(checkQuery, connection))
					{
						checkCommand.Parameters.AddWithValue("@username", username);
						int count = Convert.ToInt32(checkCommand.ExecuteScalar());

						if (count > 0)
						{
							lblMessage.Text = "Username already exists.";
							return;
						}
					}

					string hashedPassword = HashPassword(password);
					string insertQuery = @"INSERT INTO users (username, email, password, isactive, createdate) 
                                         VALUES (@username, @email, @password, @isactive, @createdate)";

					using (var command = new NpgsqlCommand(insertQuery, connection))
					{
						command.Parameters.AddWithValue("@username", username);
						command.Parameters.AddWithValue("@email", email);
						command.Parameters.AddWithValue("@password", hashedPassword);
						command.Parameters.AddWithValue("@isactive", isActive);
						command.Parameters.AddWithValue("@createdate", DateTime.Now);

						int result = command.ExecuteNonQuery();
						if (result > 0)
						{
							lblMessage.Text = "User created successfully.";
							lblMessage.ForeColor = System.Drawing.Color.Green;
							ClearForm();
							LoadUsers();
						}
						else
						{
							lblMessage.Text = "Failed to create user.";
						}
					}
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error: " + ex.Message;
			}
		}

		protected void btnUpdate_Click(object sender, EventArgs e)
		{
			int userId = Convert.ToInt32(hdnUserID.Value);
			string username = txtUsername.Text.Trim();
			string email = txtEmail.Text.Trim();
			string password = txtPassword.Text.Trim();
			bool isActive = chkIsActive.Checked;

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
			{
				lblMessage.Text = "Please fill username and email.";
				return;
			}

			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();

					string updateQuery;
					if (!string.IsNullOrEmpty(password))
					{
						string hashedPassword = HashPassword(password);
						updateQuery = @"UPDATE users SET username = @username, email = @email, 
                                      password = @password, isactive = @isactive WHERE userid = @userid";
						using (var command = new NpgsqlCommand(updateQuery, connection))
						{
							command.Parameters.AddWithValue("@username", username);
							command.Parameters.AddWithValue("@email", email);
							command.Parameters.AddWithValue("@password", hashedPassword);
							command.Parameters.AddWithValue("@isactive", isActive);
							command.Parameters.AddWithValue("@userid", userId);
							command.ExecuteNonQuery();
						}
					}
					else
					{
						updateQuery = @"UPDATE users SET username = @username, email = @email, 
                                      isactive = @isactive WHERE userid = @userid";
						using (var command = new NpgsqlCommand(updateQuery, connection))
						{
							command.Parameters.AddWithValue("@username", username);
							command.Parameters.AddWithValue("@email", email);
							command.Parameters.AddWithValue("@isactive", isActive);
							command.Parameters.AddWithValue("@userid", userId);
							command.ExecuteNonQuery();
						}
					}

					lblMessage.Text = "User updated successfully.";
					lblMessage.ForeColor = System.Drawing.Color.Green;
					ClearForm();
					LoadUsers();
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error: " + ex.Message;
			}
		}

		protected void btnCancel_Click(object sender, EventArgs e)
		{
			ClearForm();
		}

		protected void gvUsers_RowCommand(object sender, GridViewCommandEventArgs e)
		{
			int userId = Convert.ToInt32(e.CommandArgument);

			if (e.CommandName == "EditUser")
			{
				LoadUserForEdit(userId);
			}
			else if (e.CommandName == "DeleteUser")
			{
				DeleteUser(userId);
			}
		}

		private void LoadUsers()
		{
			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();
					string query = "SELECT userid, username, email, isactive, createdate FROM users ORDER BY userid";

					using (var adapter = new NpgsqlDataAdapter(query, connection))
					{
						DataTable dt = new DataTable();
						adapter.Fill(dt);
						gvUsers.DataSource = dt;
						gvUsers.DataBind();
					}
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error loading users: " + ex.Message;
			}
		}

		private void LoadUserForEdit(int userId)
		{
			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();
					string query = "SELECT userid, username, email, isactive FROM users WHERE userid = @userid";

					using (var command = new NpgsqlCommand(query, connection))
					{
						command.Parameters.AddWithValue("@userid", userId);

						using (var reader = command.ExecuteReader())
						{
							if (reader.Read())
							{
								hdnUserID.Value = reader["userid"].ToString();
								txtUsername.Text = reader["username"].ToString();
								txtEmail.Text = reader["email"].ToString();
								chkIsActive.Checked = Convert.ToBoolean(reader["isactive"]);
								txtPassword.Text = ""; // Don't show password

								btnSave.Visible = false;
								btnUpdate.Visible = true;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error loading user: " + ex.Message;
			}
		}

		private void DeleteUser(int userId)
		{
			try
			{
				using (var connection = new NpgsqlConnection(connectionString))
				{
					connection.Open();
					string deleteQuery = "DELETE FROM users WHERE userid = @userid";

					using (var command = new NpgsqlCommand(deleteQuery, connection))
					{
						command.Parameters.AddWithValue("@userid", userId);
						int result = command.ExecuteNonQuery();

						if (result > 0)
						{
							lblMessage.Text = "User deleted successfully.";
							lblMessage.ForeColor = System.Drawing.Color.Green;
							LoadUsers();
						}
						else
						{
							lblMessage.Text = "Failed to delete user.";
						}
					}
				}
			}
			catch (Exception ex)
			{
				lblMessage.Text = "Error deleting user: " + ex.Message;
			}
		}

		private void ClearForm()
		{
			txtUsername.Text = "";
			txtEmail.Text = "";
			txtPassword.Text = "";
			chkIsActive.Checked = true;
			hdnUserID.Value = "";
			btnSave.Visible = true;
			btnUpdate.Visible = false;
			lblMessage.Text = "";
			lblMessage.ForeColor = System.Drawing.Color.Red;
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
