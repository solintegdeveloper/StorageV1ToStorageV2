using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public static class StorageV2GrpcClientHelper
    {
        public static async Task<Tuple<Storage.V2.Services.Grpc.StorageV2.StorageV2Client, Metadata>> GetStorageV2Client(string server, SqlConnection db, string codigoEmpresa, string databaseInstance)
        {
            using var channel = GrpcChannel.ForAddress($"http://{server}:5100");
            var loginClient = new Storage.V2.Services.Grpc.Login.LoginClient(channel);
            var loginData = await db.GetLoginData(codigoEmpresa);
            var logInRepply = await loginClient.LogInAsync(new() 
            {
                Catalog = db.Database,
                Password = loginData.Pwd,
                User = loginData.User,
                Server = databaseInstance
            });
            //options.HttpClient?.DefaultRequestHeaders.Add("Authorization", $"Bearer {logInRepply.Token}");
            var channelV2 = GrpcChannel.ForAddress($"http://{server}:5100");
            
            var storageClient = new Storage.V2.Services.Grpc.StorageV2.StorageV2Client(channelV2);

            var metadata = new Metadata
            {
                { "Authorization", $"Bearer {logInRepply.Token}" }
            };
            

            

            return Tuple.Create(storageClient, metadata);
        }


        public static async Task<Tuple<Storage.V2.Services.Grpc.StorageV2.StorageV2Client, Metadata>> GetStorageV2Client(this MigrationProcessorConfiguration configuration)
        {
            using var channel = GrpcChannel.ForAddress($"http://{configuration.Server}:5100");
            await using var db = await DatabaseHelper.GetConnection(configuration.Server, configuration.DbAutomatizer);
            var loginClient = new Storage.V2.Services.Grpc.Login.LoginClient(channel);
            var loginData = await db.GetLoginData(configuration.CodigoEmpresa);
            var logInRepply = await loginClient.LogInAsync(new()
            {
                Catalog = db.Database,
                Password = loginData.Pwd,
                User = loginData.User,
            });
            //options.HttpClient?.DefaultRequestHeaders.Add("Authorization", $"Bearer {logInRepply.Token}");
            var channelV2 = GrpcChannel.ForAddress($"http://{configuration.Server}:5100");

            var storageClient = new Storage.V2.Services.Grpc.StorageV2.StorageV2Client(channelV2);

            var metadata = new Metadata
            {
                { "Authorization", $"Bearer {logInRepply.Token}" }
            };




            return Tuple.Create(storageClient, metadata);
        }
    }
}
