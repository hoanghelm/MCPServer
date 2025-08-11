using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UserManagement.Business
{
    /// <summary>
    /// Business logic operations for Login domain
    /// </summary>
    public interface ILoginBusiness
    {
        /// <summary>
        /// Task<string> HashPasswordAsync(string password)
        /// </summary>
        Task<string> HashPasswordAsync(string password);

    }
}
