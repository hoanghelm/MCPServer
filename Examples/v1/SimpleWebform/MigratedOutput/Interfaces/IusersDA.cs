using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UserManagement.DataAccess
{
    /// <summary>
    /// Data access operations for users table
    /// </summary>
    public interface IusersDA
    {
        /// <summary>
        /// Task<users> GetByIdAsync(int id)
        /// </summary>
        Task<users> GetByIdAsync(int id);

        /// <summary>
        /// Task<IEnumerable<users>> GetAllAsync()
        /// </summary>
        Task<IEnumerable<users>> GetAllAsync();

        /// <summary>
        /// Task<int> InsertAsync(users entity)
        /// </summary>
        Task<int> InsertAsync(users entity);

        /// <summary>
        /// Task<bool> UpdateAsync(users entity)
        /// </summary>
        Task<bool> UpdateAsync(users entity);

        /// <summary>
        /// Task<bool> DeleteAsync(int id)
        /// </summary>
        Task<bool> DeleteAsync(int id);

    }
}
