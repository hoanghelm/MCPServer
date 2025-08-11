using Microsoft.Extensions.Logging;
using SimpleWebform.Services.DataAccess;
using SimpleWebform.Services.Models;
using SimpleWebform.Services.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebform.Services.BusinessLogics
{
	/// <summary>
	/// Business logic implementation for Login operations
	/// </summary>
	public class LoginBusiness : ILoginBusiness
	{
		private readonly IUsersDA _usersDA;
		private readonly IPasswordService _passwordService;
		private readonly ILogger<LoginBusiness> _logger;

		public LoginBusiness(
			IUsersDA usersDA,
			IPasswordService passwordService,
			ILogger<LoginBusiness> logger)
		{
			_usersDA = usersDA ?? throw new ArgumentNullException(nameof(usersDA));
			_passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Authenticates a user login request
		/// </summary>
		public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest loginRequest)
		{
			try
			{
				// Validate input
				var validation = ValidateLoginRequest(loginRequest);
				if (!validation.IsSuccess)
				{
					return new AuthenticationResult
					{
						IsSuccess = false,
						Message = validation.Message
					};
				}

				// Hash password and authenticate
				string hashedPassword = _passwordService.HashPassword(loginRequest.Password);
				var user = await _usersDA.AuthenticateUserAsync(loginRequest.Username, hashedPassword);

				if (user != null)
				{
					_logger.LogInformation("User {Username} authenticated successfully", loginRequest.Username);
					return new AuthenticationResult
					{
						IsSuccess = true,
						Message = "Authentication successful",
						User = user
					};
				}

				_logger.LogWarning("Authentication failed for user {Username}", loginRequest.Username);
				return new AuthenticationResult
				{
					IsSuccess = false,
					Message = "Invalid username or password"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during authentication for user {Username}", loginRequest.Username);
				return new AuthenticationResult
				{
					IsSuccess = false,
					Message = "An error occurred during authentication"
				};
			}
		}

		/// <summary>
		/// Validates login credentials format
		/// </summary>
		public OperationResult ValidateLoginRequest(LoginRequest loginRequest)
		{
			if (loginRequest == null)
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "Login request cannot be null"
				};
			}

			if (string.IsNullOrEmpty(loginRequest.Username?.Trim()) ||
				string.IsNullOrEmpty(loginRequest.Password?.Trim()))
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "Please enter username and password"
				};
			}

			return new OperationResult { IsSuccess = true };
		}
	}

	/// <summary>
	/// Business logic implementation for User operations
	/// </summary>
	public class UserBusiness : IUserBusiness
	{
		private readonly IUsersDA _usersDA;
		private readonly IPasswordService _passwordService;
		private readonly ILogger<UserBusiness> _logger;

		public UserBusiness(
			IUsersDA usersDA,
			IPasswordService passwordService,
			ILogger<UserBusiness> logger)
		{
			_usersDA = usersDA ?? throw new ArgumentNullException(nameof(usersDA));
			_passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Gets all users
		/// </summary>
		public async Task<IEnumerable<User>> GetAllUsersAsync()
		{
			try
			{
				return await _usersDA.GetAllUsersAsync();
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
		public async Task<User?> GetUserByIdAsync(int userId)
		{
			try
			{
				return await _usersDA.GetUserByIdAsync(userId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting user by ID {UserId}", userId);
				throw;
			}
		}

		/// <summary>
		/// Creates a new user
		/// </summary>
		public async Task<OperationResult> CreateUserAsync(User user)
		{
			try
			{
				// Validate user data
				var validation = ValidateUser(user);
				if (!validation.IsSuccess)
				{
					return validation;
				}

				// Check if username already exists
				bool usernameExists = await _usersDA.UsernameExistsAsync(user.Username);
				if (usernameExists)
				{
					return new OperationResult
					{
						IsSuccess = false,
						Message = "Username already exists"
					};
				}

				// Hash password and set creation date
				user.Password = _passwordService.HashPassword(user.Password);
				user.CreateDate = DateTime.Now;

				// Create user
				bool success = await _usersDA.CreateUserAsync(user);
				if (success)
				{
					_logger.LogInformation("User {Username} created successfully", user.Username);
					return new OperationResult
					{
						IsSuccess = true,
						Message = "User created successfully"
					};
				}

				return new OperationResult
				{
					IsSuccess = false,
					Message = "Failed to create user"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating user {Username}", user.Username);
				return new OperationResult
				{
					IsSuccess = false,
					Message = "An error occurred while creating the user"
				};
			}
		}

		/// <summary>
		/// Updates an existing user
		/// </summary>
		public async Task<OperationResult> UpdateUserAsync(User user)
		{
			try
			{
				// Validate user data
				var validation = ValidateUser(user, isUpdate: true);
				if (!validation.IsSuccess)
				{
					return validation;
				}

				// Hash password if provided
				if (!string.IsNullOrEmpty(user.Password))
				{
					user.Password = _passwordService.HashPassword(user.Password);
				}

				// Update user
				bool success = await _usersDA.UpdateUserAsync(user);
				if (success)
				{
					_logger.LogInformation("User {UserId} updated successfully", user.UserId);
					return new OperationResult
					{
						IsSuccess = true,
						Message = "User updated successfully"
					};
				}

				return new OperationResult
				{
					IsSuccess = false,
					Message = "Failed to update user"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating user {UserId}", user.UserId);
				return new OperationResult
				{
					IsSuccess = false,
					Message = "An error occurred while updating the user"
				};
			}
		}

		/// <summary>
		/// Deletes a user
		/// </summary>
		public async Task<OperationResult> DeleteUserAsync(int userId)
		{
			try
			{
				bool success = await _usersDA.DeleteUserAsync(userId);
				if (success)
				{
					_logger.LogInformation("User {UserId} deleted successfully", userId);
					return new OperationResult
					{
						IsSuccess = true,
						Message = "User deleted successfully"
					};
				}

				return new OperationResult
				{
					IsSuccess = false,
					Message = "Failed to delete user"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting user {UserId}", userId);
				return new OperationResult
				{
					IsSuccess = false,
					Message = "An error occurred while deleting the user"
				};
			}
		}

		/// <summary>
		/// Validates user data
		/// </summary>
		public OperationResult ValidateUser(User user, bool isUpdate = false)
		{
			if (user == null)
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "User data cannot be null"
				};
			}

			if (string.IsNullOrEmpty(user.Username?.Trim()) ||
				string.IsNullOrEmpty(user.Email?.Trim()))
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "Please fill username and email"
				};
			}

			// For new users, password is required
			if (!isUpdate && string.IsNullOrEmpty(user.Password?.Trim()))
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "Please fill all fields"
				};
			}

			// Basic email validation
			if (!user.Email.Contains("@"))
			{
				return new OperationResult
				{
					IsSuccess = false,
					Message = "Please enter a valid email address"
				};
			}

			return new OperationResult { IsSuccess = true };
		}
	}
}
