using SimpleWebform.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebform.Services.DataAccess
{
	/// <summary>
	/// Data access operations for users table
	/// </summary>
	public interface IUsersDA
	{
		/// <summary>
		/// Authenticates a user with username and password
		/// </summary>
		/// <param name="username">Username</param>
		/// <param name="hashedPassword">Hashed password</param>
		/// <returns>User if authentication successful, null otherwise</returns>
		Task<User?> AuthenticateUserAsync(string username, string hashedPassword);

		/// <summary>
		/// Gets all users from the database
		/// </summary>
		/// <returns>List of users</returns>
		Task<IEnumerable<User>> GetAllUsersAsync();

		/// <summary>
		/// Gets a user by ID
		/// </summary>
		/// <param name="userId">User ID</param>
		/// <returns>User if found, null otherwise</returns>
		Task<User?> GetUserByIdAsync(int userId);

		/// <summary>
		/// Gets a user by username
		/// </summary>
		/// <param name="username">Username</param>
		/// <returns>User if found, null otherwise</returns>
		Task<User?> GetUserByUsernameAsync(string username);

		/// <summary>
		/// Creates a new user
		/// </summary>
		/// <param name="user">User to create</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> CreateUserAsync(User user);

		/// <summary>
		/// Updates an existing user
		/// </summary>
		/// <param name="user">User to update</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> UpdateUserAsync(User user);

		/// <summary>
		/// Deletes a user
		/// </summary>
		/// <param name="userId">User ID to delete</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> DeleteUserAsync(int userId);

		/// <summary>
		/// Checks if a username exists
		/// </summary>
		/// <param name="username">Username to check</param>
		/// <returns>True if exists, false otherwise</returns>
		Task<bool> UsernameExistsAsync(string username);
	}
}
