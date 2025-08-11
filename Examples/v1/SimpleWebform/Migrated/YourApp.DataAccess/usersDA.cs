using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UserManagement.DataAccess
{
    public class usersDA : IusersDA
    {
        private readonly ILogger<usersDA> _logger;
        private readonly string _connectionString;

        public usersDA(ILogger<usersDA> logger, string connectionString)
        {
            _logger = logger;
            _connectionString = connectionString;
        }

        public Task<users> GetByIdAsync(int id)
        {
            // TODO: Implement database operation
            throw new NotImplementedException();
        }

        public Task<IEnumerable<users>> GetAllAsync()
        {
            // TODO: Implement database operation
            throw new NotImplementedException();
        }

        public Task<int> InsertAsync(users entity)
        {
            // TODO: Implement database operation
            throw new NotImplementedException();
        }

        public Task<bool> UpdateAsync(users entity)
        {
            // TODO: Implement database operation
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(int id)
        {
            // TODO: Implement database operation
            throw new NotImplementedException();
        }

    }
}
