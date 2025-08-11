using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebform.Services.Models
{
	/// <summary>
	/// Represents a user entity
	/// </summary>
	public class User
	{
		public int UserId { get; set; }
		public string Username { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public DateTime CreateDate { get; set; }
	}

	/// <summary>
	/// Data transfer object for user authentication
	/// </summary>
	public class LoginRequest
	{
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	/// <summary>
	/// Data transfer object for authentication result
	/// </summary>
	public class AuthenticationResult
	{
		public bool IsSuccess { get; set; }
		public string Message { get; set; } = string.Empty;
		public User User { get; set; }
	}

	/// <summary>
	/// Data transfer object for user operations result
	/// </summary>
	public class OperationResult
	{
		public bool IsSuccess { get; set; }
		public string Message { get; set; } = string.Empty;
		public object Data { get; set; }
	}
}
