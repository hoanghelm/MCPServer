using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SimpleWebform.Services.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SimpleWebform.Services.DataAccess
{
	/// <summary>
	/// PostgreSQL implementation of users data access
	/// </summary>
	public class UsersDA : IUsersDA
	{
		private readonly string _connectionString;
		private readonly ILogger<UsersDA> _logger;

		public UsersDA(IConfiguration configuration, ILogger<UsersDA> logger)
		{
			_connectionString = configuration.GetConnectionString("PostgreSQLConnection")
				?? throw new ArgumentNullException(nameof(configuration));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Authenticates a user with username and password
		/// </summary>
		public async Task<User> AuthenticateUserAsync(string username, string hashedPassword)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string query = @"
                    SELECT userid, username, email, isactive 
                    FROM users 
                    WHERE username = @username AND password = @password AND isactive = true";

					using (var command = new NpgsqlCommand(query, connection))
					{
						command.Parameters.AddWithValue("@username", username);
						command.Parameters.AddWithValue("@password", hashedPassword);

						using (var reader = await command.ExecuteReaderAsync())
						{
							if (await reader.ReadAsync())
							{
								return new User
								{
									UserId = reader.GetInt32(0),
									Username = reader.GetString(1),
									Email = reader.GetString(2),
									IsActive = reader.GetBoolean(3)
								};
							}
							return null;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error authenticating user {Username}", username);
				throw;
			}
		}

		/// <summary>
		/// Gets all users from the database
		/// </summary>
		public async Task<IEnumerable<User>> GetAllUsersAsync()
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string query = @"
                    SELECT userid, username, email, isactive, createdate 
                    FROM users 
                    ORDER BY userid";

					using (var command = new NpgsqlCommand(query, connection))
					{
						using (var reader = await command.ExecuteReaderAsync())
						{
							var users = new List<User>();
							while (await reader.ReadAsync())
							{
								users.Add(new User
								{
									UserId = reader.GetInt32(0),
									Username = reader.GetString(1),
									Email = reader.GetString(2),
									IsActive = reader.GetBoolean(3),
									CreateDate = reader.GetDateTime(4)
								});
							}
							return users;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting all users");
				throw;
			}
		}

		/// <summary>
		/// Gets a user by ID
		/// </summary>
		public async Task<User> GetUserByIdAsync(int userId)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string query = @"
                    SELECT userid, username, email, isactive, createdate 
                    FROM users 
                    WHERE userid = @userid";

					using (var command = new NpgsqlCommand(query, connection))
					{
						command.Parameters.AddWithValue("@userid", userId);

						using (var reader = await command.ExecuteReaderAsync())
						{
							if (await reader.ReadAsync())
							{
								return new User
								{
									UserId = reader.GetInt32(0),
									Username = reader.GetString(1),
									Email = reader.GetString(2),
									IsActive = reader.GetBoolean(3),
									CreateDate = reader.GetDateTime(4)
								};
							}
							return null;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting user by ID {UserId}", userId);
				throw;
			}
		}

		/// <summary>
		/// Gets a user by username
		/// </summary>
		public async Task<User> GetUserByUsernameAsync(string username)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string query = @"
                    SELECT userid, username, email, isactive, createdate 
                    FROM users 
                    WHERE username = @username";

					using (var command = new NpgsqlCommand(query, connection))
					{
						command.Parameters.AddWithValue("@username", username);

						using (var reader = await command.ExecuteReaderAsync())
						{
							if (await reader.ReadAsync())
							{
								return new User
								{
									UserId = reader.GetInt32(0),
									Username = reader.GetString(1),
									Email = reader.GetString(2),
									IsActive = reader.GetBoolean(3),
									CreateDate = reader.GetDateTime(4)
								};
							}
							return null;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting user by username {Username}", username);
				throw;
			}
		}

		/// <summary>
		/// Creates a new user
		/// </summary>
		public async Task<bool> CreateUserAsync(User user)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string insertQuery = @"
                    INSERT INTO users (username, email, password, isactive, createdate) 
                    VALUES (@username, @email, @password, @isactive, @createdate)";

					using (var command = new NpgsqlCommand(insertQuery, connection))
					{
						command.Parameters.AddWithValue("@username", user.Username);
						command.Parameters.AddWithValue("@email", user.Email);
						command.Parameters.AddWithValue("@password", user.Password);
						command.Parameters.AddWithValue("@isactive", user.IsActive);
						command.Parameters.AddWithValue("@createdate", user.CreateDate);

						int result = await command.ExecuteNonQueryAsync();
						return result > 0;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating user {Username}", user.Username);
				throw;
			}
		}

		/// <summary>
		/// Updates an existing user
		/// </summary>
		public async Task<bool> UpdateUserAsync(User user)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					string updateQuery;
					if (!string.IsNullOrEmpty(user.Password))
					{
						updateQuery = @"
                        UPDATE users 
                        SET username = @username, email = @email, password = @password, isactive = @isactive 
                        WHERE userid = @userid";
					}
					else
					{
						updateQuery = @"
                        UPDATE users 
                        SET username = @username, email = @email, isactive = @isactive 
                        WHERE userid = @userid";
					}

					using (var command = new NpgsqlCommand(updateQuery, connection))
					{
						command.Parameters.AddWithValue("@username", user.Username);
						command.Parameters.AddWithValue("@email", user.Email);
						command.Parameters.AddWithValue("@isactive", user.IsActive);
						command.Parameters.AddWithValue("@userid", user.UserId);

						if (!string.IsNullOrEmpty(user.Password))
						{
							command.Parameters.AddWithValue("@password", user.Password);
						}

						int result = await command.ExecuteNonQueryAsync();
						return result > 0;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating user {UserId}", user.UserId);
				throw;
			}
		}

		/// <summary>
		/// Deletes a user
		/// </summary>
		public async Task<bool> DeleteUserAsync(int userId)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string deleteQuery = "DELETE FROM users WHERE userid = @userid";

					using (var command = new NpgsqlCommand(deleteQuery, connection))
					{
						command.Parameters.AddWithValue("@userid", userId);

						int result = await command.ExecuteNonQueryAsync();
						return result > 0;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting user {UserId}", userId);
				throw;
			}
		}

		/// <summary>
		/// Checks if a username exists
		/// </summary>
		public async Task<bool> UsernameExistsAsync(string username)
		{
			try
			{
				using (var connection = new NpgsqlConnection(_connectionString))
				{
					await connection.OpenAsync();

					const string checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username";

					using (var command = new NpgsqlCommand(checkQuery, connection))
					{
						command.Parameters.AddWithValue("@username", username);

						var count = await command.ExecuteScalarAsync();
						return Convert.ToInt32(count) > 0;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking if username exists {Username}", username);
				throw;
			}
		}
	}
}