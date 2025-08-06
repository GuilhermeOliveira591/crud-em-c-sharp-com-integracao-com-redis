using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Shared.Repository
{
    public interface ISqlConnectionFactory
    {
        Task<SqlConnection> OpenConnectionAsync();
    }
}
