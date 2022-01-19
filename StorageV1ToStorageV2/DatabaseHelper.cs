using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public static class DatabaseHelper
    {
        public static async Task<SqlConnection>  GetConnection(string server, string database)
        {
            var con = new SqlConnection($"Server={server};Database={database};Trusted_Connection=True;MultipleActiveResultSets=True");
            await con.OpenAsync();
            return con;
        }

        public static async Task TryExecute(this SqlConnection db, string sql)
        {
            try
            {
                await db.ExecuteAsync(sql);
            }
            catch (Exception)
            {

            }
            
        }
    }
}
