using SimpleWebform.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebform.Services.BusinessLogics
{
	/// <summary>
	/// Business logic operations for Login domain
	/// </summary>
	public interface ILoginBusiness
	{
		/// <summary>
		/// Authenticates a user login request
		/// </summary>
		/// <param name="loginRequest">Login credentials</param>
		/// <returns>Authentication result</returns>
		Task<AuthenticationResult> AuthenticateAsync(LoginRequest loginRequest);

		/// <summary>
		/// Validates login credentials format
		/// </summary>
		/// <param name="loginRequest">Login request to validate</param>
		/// <returns>Validation result</returns>
		OperationResult ValidateLoginRequest(LoginRequest loginRequest);
	}

	/// <summary>
	/// Business logic operations for User domain
	/// </summary>
	public interface IUserBusiness
	{
		/// <summary>
		/// Gets all users
		/// </summary>
		/// <returns>List of users</returns>
		Task<IEnumerable<User>> GetAllUsersAsync();

		/// <summary>
		/// Gets a user by ID
		/// </summary>
		/// <param name="userId">User ID</param>
		/// <returns>User if found</returns>
		Task<User?> GetUserByIdAsync(int userId);

		/// <summary>
		/// Creates a new user
		/// </summary>
		/// <param name="user">User to create</param>
		/// <returns>Operation result</returns>
		Task<OperationResult> CreateUserAsync(User user);

		/// <summary>
		/// Updates an existing user
		/// </summary>
		/// <param name="user">User to update</param>
		/// <returns>Operation result</returns>
		Task<OperationResult> UpdateUserAsync(User user);

		/// <summary>
		/// Deletes a user
		/// </summary>
		/// <param name="userId">User ID</param>
		/// <returns>Operation result</returns>
		Task<OperationResult> DeleteUserAsync(int userId);

		/// <summary>
		/// Validates user data
		/// </summary>
		/// <param name="user">User to validate</param>
		/// <param name="isUpdate">Whether this is an update operation</param>
		/// <returns>Validation result</returns>
		OperationResult ValidateUser(User user, bool isUpdate = false);
	}
}
