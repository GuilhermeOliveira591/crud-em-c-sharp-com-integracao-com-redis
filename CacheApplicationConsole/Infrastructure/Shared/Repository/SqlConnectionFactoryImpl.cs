using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Shared.Repository
{
    public class SqlConnectionFactoryImpl : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactoryImpl(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<SqlConnection> OpenConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
