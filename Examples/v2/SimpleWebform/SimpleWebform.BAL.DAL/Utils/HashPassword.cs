using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWebform.Services.Utils
{
	/// <summary>
	/// Service for password hashing operations
	/// </summary>
	public interface IPasswordService
	{
		/// <summary>
		/// Hashes a password using SHA256
		/// </summary>
		/// <param name="password">Plain text password</param>
		/// <returns>Hashed password</returns>
		string HashPassword(string password);

		/// <summary>
		/// Verifies a password against a hash
		/// </summary>
		/// <param name="password">Plain text password</param>
		/// <param name="hashedPassword">Hashed password</param>
		/// <returns>True if password matches</returns>
		bool VerifyPassword(string password, string hashedPassword);
	}

	/// <summary>
	/// Implementation of password hashing service
	/// </summary>
	public class PasswordService : IPasswordService
	{
		/// <summary>
		/// Hashes a password using SHA256
		/// </summary>
		/// <param name="password">Plain text password</param>
		/// <returns>Hashed password</returns>
		public string HashPassword(string password)
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

		/// <summary>
		/// Verifies a password against a hash
		/// </summary>
		/// <param name="password">Plain text password</param>
		/// <param name="hashedPassword">Hashed password</param>
		/// <returns>True if password matches</returns>
		public bool VerifyPassword(string password, string hashedPassword)
		{
			string hashedInput = HashPassword(password);
			return string.Equals(hashedInput, hashedPassword, StringComparison.OrdinalIgnoreCase);
		}
	}
}
