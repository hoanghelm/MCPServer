using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UserManagement.Business
{
    /// <summary>
    /// Business logic operations for User domain
    /// </summary>
    public interface IUserBusiness
    {
        /// <summary>
        /// Task gvUsers_RowCommandAsync()
        /// </summary>
        Task gvUsers_RowCommandAsync();

        /// <summary>
        /// Task<string> HashPasswordAsync(string password)
        /// </summary>
        Task<string> HashPasswordAsync(string password);

    }
}
