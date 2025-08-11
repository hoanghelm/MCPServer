using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UserManagement.Business
{
    public class LoginBusiness : ILoginBusiness
    {
        private readonly ILogger<LoginBusiness> _logger;
        private readonly IusersDA _usersDADA;

        public LoginBusiness(ILogger<LoginBusiness> logger
            , IusersDA usersDADA
        )
        {
            _logger = logger;
            _usersDADA = usersDADA;
        }

        public Task<string> HashPasswordAsync(string password)
        {
            // TODO: Implement business logic
            throw new NotImplementedException();
        }

    }
}
