using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EShop.Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(long userId, string username);
    }
}
