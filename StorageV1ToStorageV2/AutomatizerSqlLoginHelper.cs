
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public class LoginData
    {
        public string? User { get; set; }
        public string? Pwd { get; set; }
    }

    public static class AutomatizerSqlLoginHelper
    {
        public static async Task<LoginData> GetLoginData(this SqlConnection db, string codigoEmpresa)
        {
            var data = await db.QueryFirstOrDefaultAsync<LoginData>("SELECT Top 1 SgrUsr As [User], SgrClvUsr As [Pwd] FROM SgrUsr WHERE EmpCod = @codigoEmpresa",
                new { codigoEmpresa });
            if (data == null) throw new InvalidOperationException("No se encuentra usuario");
            return data;
        }
    }
}
