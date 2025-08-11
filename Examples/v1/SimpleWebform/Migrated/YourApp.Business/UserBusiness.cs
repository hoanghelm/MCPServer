using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UserManagement.Business
{
    public class UserBusiness : IUserBusiness
    {
        private readonly ILogger<UserBusiness> _logger;
        private readonly IusersDA _usersDADA;

        public UserBusiness(ILogger<UserBusiness> logger
            , IusersDA usersDADA
        )
        {
            _logger = logger;
            _usersDADA = usersDADA;
        }

        public Task gvUsers_RowCommandAsync()
        {
            // TODO: Implement business logic
            throw new NotImplementedException();
        }

        public Task<string> HashPasswordAsync(string password)
        {
            // TODO: Implement business logic
            throw new NotImplementedException();
        }

    }
}
