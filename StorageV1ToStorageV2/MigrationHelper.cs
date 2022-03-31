using Dapper;
using Grpc.Core;
using Microsoft.Data.SqlClient;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public static class MigrationHelper
    {
        private static ProgressBarOptions ChildOptions = new ProgressBarOptions
        {
            ForegroundColor = ConsoleColor.Green,
            BackgroundColor = ConsoleColor.DarkGreen,
            ProgressCharacter = '─'
        };

        private enum DocumentsColumns
        {
            Id,
            Title,
            Description,
            Thumbnail,
            Private,
            ReadOnly,
            UserOwner,
            Extension,
            Size,
            TypeDoc,
            Origin,
            DateCreated,
            DateUpdated,
            Hash,
            IsPasswordProtected,
            IdParent,
            IdRootParent,
            FullPath,
            FullIdsPath,
            IdArchivoBlob,
        }

        private enum FoldersColumns
        {
            Id,
            Name,
            IdParent,
            IsRoot,
            FolderIcon,
            EmpCod,
            ModIde,
            TrnCod,
            TrnNum,
            FechaCreacion,
            IdRootParent,
            FullPath,
            FullIdsPath
        }

        public static async Task Migrate(this V1.StorageAccount account, ProgressBar pgrRoot,  string server)
        {
            await using var automatizerDb = await DatabaseHelper.GetConnection(server, account.GetDbNameFromAccount());
            await using var blobsV1Db = await DatabaseHelper.GetConnection(server, account.IdCuenta.ToString());
            var codigosEmpresas = await GetCodigosEmpresasDeContenedores(blobsV1Db, automatizerDb.Database);
            var filetableDbRootPath = await blobsV1Db.ExecuteScalarAsync<string>("SELECT FileTableRootPath()");
            //await blobsV1Db.TryExecute("ALTER DATABASE [449ea434-e63f-4242-903d-24b614628e63] SET FILESTREAM( NON_TRANSACTED_ACCESS = FULL ) WITH NO_WAIT");
            using var empresasPbar = pgrRoot.Spawn(codigosEmpresas.Count(), "Migrando Empresas", ChildOptions);
            foreach (var codigoEmpresa in codigosEmpresas)
            {
                empresasPbar.Tick($"Migrando Empresa {codigoEmpresa}");

                var filetableRootPath = $"{filetableDbRootPath}\\DocumentosAdjuntos{automatizerDb.Database}{codigoEmpresa}\\";
                var numeroCarpetasEstimadas = await automatizerDb.ExecuteScalarAsync<int>(@"SELECT COUNT(*)
                FROM DocumentService_Folders WHERE EmpCod = @codigoEmpresa ", new { codigoEmpresa });
                using var foldersReader = await automatizerDb.ExecuteReaderAsync(@"SELECT *
                FROM DocumentService_Folders WHERE EmpCod = @codigoEmpresa ORDER BY FechaCreacion DESC", new { codigoEmpresa });
                using var empresaPbar = empresasPbar.Spawn(numeroCarpetasEstimadas, "Migrando Carpetas de Transacciones", ChildOptions);
                var serverName = await automatizerDb.ExecuteScalarAsync<string>("select SERVERPROPERTY('MachineName')");
                var (clientV2, metadada) = await StorageV2GrpcClientHelper.GetStorageV2Client(serverName, automatizerDb, codigoEmpresa, server);

                //Creamos indice no existente y necesario para buscar archivo por nombre
                await blobsV1Db.TryExecute(@$"
                CREATE NONCLUSTERED INDEX [IX_BlockBlobsDocumentosAdjuntos{automatizerDb.Database}{codigoEmpresa}_Name]
                ON [dbo].[BlockBlobsDocumentosAdjuntos{automatizerDb.Database}{codigoEmpresa}] ([Name])
");

                long idFolder = 0;
                var sqlSelectFileName = $"SELECT  CAST(IdBlockBlob as varchar(100)) As [FileName] FROM BlockBlobsDocumentosAdjuntos{automatizerDb.Database}{codigoEmpresa} Where Name = @Name";
                while (await foldersReader.ReadAsync())
                {
                    empresaPbar.Tick($"Migrando {foldersReader.GetString((int)FoldersColumns.FullPath)}");
                    using var filesReader = await automatizerDb.ExecuteReaderAsync(@"SELECT * FROM DocumentService_Documents 
                    WHERE IdParent = @IdParent",
                        new { IdParent = foldersReader.GetInt64(0) });
                    var numeroArchivosMigrados = 0;

                    var filesToClear = new List<long>();

                    while (await filesReader.ReadAsync())
                    {
                        var idBlobBlock = await blobsV1Db.ExecuteScalarAsync<string?>(sqlSelectFileName, new { Name = filesReader.GetGuid((int)DocumentsColumns.IdArchivoBlob).ToString() });
                        if (string.IsNullOrWhiteSpace(idBlobBlock))
                        {
                            continue;
                        }
                        var fileName = Path.Combine(filetableRootPath, idBlobBlock
                            );
                        if (File.Exists(fileName))
                        {
                            try
                            {
                                var blobGrpup = await CopyFileToV2(fileName, clientV2, metadada, codigoEmpresa, filesReader);
                                if (idFolder == 0)
                                {
                                    idFolder = await SaveFolderToDocserviceV2(foldersReader, automatizerDb);
                                }
                                var idFile = await SaveFileToDocserviceV2(filesReader, automatizerDb, idFolder, blobGrpup);
                                numeroArchivosMigrados++;
                                filesToClear.Add(idFile);
                            }
                            catch (Exception ex)
                            {
                                empresaPbar.WriteErrorLine(ex.Message);
                                await Task.Delay(2000);
                            }
                            
                        }
                    }
                    await filesReader.CloseAsync();
                    foreach (var fileId in filesToClear)
                    {
                        await automatizerDb.ExecuteAsync(@"DELETE FROM DocumentService_Documents WHERE Id = @Id", new { Id = fileId });
                    }
                    idFolder = 0;
                    //empresaPbar.Tick();
                }
                //empresasPbar.Tick();
            }
        }


        private static async Task<long> SaveFolderToDocserviceV2(DbDataReader folderReader, SqlConnection db)
        {
            var x = await db.QueryFirstAsync<FolderV2Model>("DocumentServiceV2_AddFolder", new 
            {
                Name = folderReader.GetString((int)FoldersColumns.Name),
                IdParent = 0,
                IsRoot = true,
                FolderIcon = folderReader.GetFieldValue<byte[]>( (int)FoldersColumns.FolderIcon),
                EmpCod = folderReader.GetString( (int)FoldersColumns.EmpCod),
                ModIde = folderReader.GetByte( (int)FoldersColumns.ModIde),
                TrnCod = folderReader.GetString( (int)FoldersColumns.TrnCod),
                TrnNum = folderReader.GetInt32( (int)FoldersColumns.TrnNum),
                IdRootParent = 0,
                FullPath = $"\\{folderReader.GetString((int)FoldersColumns.Name)}\\",
                FullIdsPath = ""
            }, commandType:System.Data.CommandType.StoredProcedure);
            return x.Id;
        }

        private static async Task<long> SaveFileToDocserviceV2( DbDataReader documentReader, SqlConnection db, long idFolder, string blobGroup)
        {
            var id = await db.ExecuteScalarAsync<long>("DocumentServiceV2_AddFile", new
            {
                Title = documentReader.GetString((int)DocumentsColumns.Title),
                Description = documentReader.GetString((int)DocumentsColumns.Description),
                Private= false,
                ReadOnly =false,
                UserOwner = documentReader.GetString((int)DocumentsColumns.UserOwner ) ,
                Extension = documentReader.GetString((int)DocumentsColumns.Extension),
                Size = documentReader.GetInt64((int)DocumentsColumns.Size),
                TypeDoc = documentReader.GetByte((int)DocumentsColumns.TypeDoc),
                Origin= documentReader.GetByte((int)DocumentsColumns.Origin),
                DateCreated = documentReader.GetDateTime((int)DocumentsColumns.DateCreated),
                DateUpdated = DateTime.Now,
                Hash = documentReader.GetString((int)DocumentsColumns.Hash),
                IsPasswordProtected=false,
                IdParent = idFolder,
                IdRootParent = idFolder,
                IdArchivoBlob = documentReader.GetGuid((int)DocumentsColumns.IdArchivoBlob),
                BlobGroup = blobGroup,
            }, commandType: System.Data.CommandType.StoredProcedure);
            return id;
        }

        private static async Task<string> CopyFileToV2(string file, 
            Storage.V2.Services.Grpc.StorageV2.StorageV2Client client, 
            Metadata metadata,
            string codigoEmpresa,
            DbDataReader fileDataReader)
        {
            var fileInfo = new FileInfo(file);
            var fileDate = fileDataReader.GetDateTime((int)DocumentsColumns.DateCreated);
            var containerName = $"DocumentosAutomatizer-{codigoEmpresa}-{fileDate.ToString("yyyy-MM")}";
            var containerResponse = await client.GetContainerAsync(new ()
            {
                Name = containerName,
            }, metadata);

            if (!containerResponse.Response.Status)
            {
                throw new Exception($"Error al obtener la referencia al contenedor. Mensaje:{containerResponse.Response.Message}");
            }
            
            var blob = await client.GetBlobAsync(new() 
            {
                Container = containerName,
                Name = fileDataReader.GetGuid((int)DocumentsColumns.IdArchivoBlob).ToString(),
                
            }, metadata);

            if (!blob.Response.Status)
            {
                throw new Exception($"Error al obtener la referencia al blob. Mensaje:{blob.Response.Message}");
            }

            using var stream = fileInfo.OpenRead();
            await UpdateBlobData(client, metadata, blob.Blob, stream);
            return fileDate.ToString("yyyy-MM");
        }

        private static async Task UpdateBlobData(Storage.V2.Services.Grpc.StorageV2.StorageV2Client client,
            Metadata metadata,
            Storage.V2.Services.Grpc.Blob blob, 
            Stream stream)
        {
            var buffer = new byte[60 * 1024];
            long length = stream.Length;
            long read = buffer.Length;
            long readed = 0;
            bool continuar = true;
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            var storageStream = client.SendBlobData(metadata);
            var blobkId = 1;
            var blocsCount = Convert.ToInt32(Math.Ceiling(length * 1.0m / buffer.Length));
            while (continuar)
            {
                if (readed + read > length)
                {
                    read = length - readed;
                    continuar = false;
                }
                buffer = new byte[read];
                stream.Read(buffer, 0, buffer.Length);
                readed += read;
                //progress?.Report(Math.Floor(readed * 1.0 / length * 1.0 * 100.0));
                //ProgresoCargaArchivo = Convert.ToInt32(Math.Floor(readed * 1.0 / length * 1.0 * 100.0));
                await storageStream.RequestStream.WriteAsync(new ()
                {
                    AccountId = blob.AccountId,
                    ContainerId = blob.ContainerId,
                    BlobId = blob.BlobId,
                    BlobBlockId = blobkId,
                    BlobBlocksCount = blocsCount,
                    Data = Google.Protobuf.ByteString.CopyFrom(buffer)
                });
                
                await storageStream.ResponseStream.MoveNext();
                

                //_blobClient.StreamBlockToServer(blobBlockEventArgs.IdFile, buffer, !continuar);
                blobkId++;
            }
            await storageStream.RequestStream.CompleteAsync();
            
        }


        public static async Task<List<string>> GetCodigosEmpresasDeContenedores(this SqlConnection db, string automatizerDbName)
        {
            var codigos = await db.QueryAsync<string>(@$"SELECT REPLACE([Name], 'DocumentosAdjuntos{automatizerDbName}', '') 
            FROM sys.tables WHERE Name Like 'DocumentosAdjuntos{automatizerDbName}%'");
            return codigos.ToList();
        }

        public static string GetDbNameFromAccount(this V1.StorageAccount account)
        {
            if (account.Nombre == null) throw new ArgumentNullException(nameof(account));
            return account.Nombre[21..];
        }
    }
}
