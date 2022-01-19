using Dapper;
using Grpc.Core;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public class MigrationProcessor
    {
        private readonly MigrationProcessorConfiguration configuration;

        public int NumberOkWorkers { get; set; } = Environment.ProcessorCount / 2;
        
        private ConcurrentBag<FolderMigrationWork> Works { get; set; }

        public MigrationProcessor(MigrationProcessorConfiguration configuration)
        {
            Works = new ConcurrentBag<FolderMigrationWork>();
            this.configuration = configuration;
        }

        public void AddWork(FolderMigrationWork work)
        {
            Works.Add(work);
        }

        public async Task Run()
        {
            var tasks = new List<Task>(NumberOkWorkers +1);
            for (int i = 0; i < NumberOkWorkers; i++)
            {
                tasks.Add(Task.Factory.StartNew(async ()=> await Work(Works, configuration) ));
            }
            await Task.WhenAll(tasks);
        }

        private static async Task Work(ConcurrentBag<FolderMigrationWork> works, MigrationProcessorConfiguration configuration)
        {
            var (storageV2Client, metadata) = await configuration.GetStorageV2Client();
            await using var automatizerDb = await DatabaseHelper.GetConnection(configuration.DbServer,
                configuration.DbAutomatizer);
            await using var blobsV1Db = 
                await DatabaseHelper.GetConnection(configuration.DbServer, configuration.DbBlobsV1);

            var sqlSelectFileName = $"SELECT  CAST(IdBlockBlob as varchar(100)) As [FileName] FROM BlockBlobsDocumentosAdjuntos{automatizerDb.Database}{configuration.CodigoEmpresa} Where Name = @Name";

            while (!works.IsEmpty)
            {
                if(works.TryTake(out var work))
                {
                    long folderId = 0; // 
                    foreach (var document in work.Documents)
                    {
                        var idBlobBlock = await blobsV1Db.ExecuteScalarAsync<string?>(sqlSelectFileName, new { Name = document.IdArchivoBlob });
                        if (string.IsNullOrWhiteSpace(idBlobBlock))
                        {
                            continue;
                        }
                        var fileName = Path.Combine(configuration.FiletableRootPath, idBlobBlock
                            );
                        if (File.Exists(fileName))
                        {
                            if(folderId == 0)
                            {
                                folderId = await SaveFolderToDocserviceV2(work.Folder, automatizerDb);
                            }
                            var blobGrpup = 
                                await CopyFileToV2(fileName, 
                                storageV2Client, 
                                metadata, 
                                configuration.CodigoEmpresa, document);
                            await SaveFileToDocserviceV2(document, automatizerDb, folderId, blobGrpup);
                        }
                    }
                }
                await Task.Delay(1);
            }
        }


        private static async Task<long> SaveFolderToDocserviceV2(V1.StorageFolder folder, SqlConnection db)
        {
            var x = await db.QueryFirstAsync<FolderV2Model>("DocumentServiceV2_AddFolder", new
            {
                folder.Name,
                folder.IdParent,
                folder.IsRoot,
                folder.FolderIcon,
                folder.EmpCod,
                folder.ModIde,
                folder.TrnCod,
                folder.TrnNum,
                folder.IdRootParent,
                folder.FullPath,
                FullIdsPath = ""
            }, commandType: System.Data.CommandType.StoredProcedure);
            return x.Id;
        }


        private static async Task SaveFileToDocserviceV2(V1.StorageDocument document, SqlConnection db, long idFolder, string blobGroup)
        {
            await db.ExecuteScalarAsync<long>("DocumentServiceV2_AddFile", new
            {
                document.Title,
                document.Description,
                document.Private,
                document.ReadOnly,
                document.UserOwner,
                document.Extension,
                document.Size,
                document.TypeDoc,
                document.Origin,
                document.DateCreated,
                DateUpdated =  DateTime.Now,
                document.Hash,
                document.IsPasswordProtected,
                IdParent = idFolder,
                IdRootParent = idFolder,
                document.IdArchivoBlob,
                BlobGroup = blobGroup,
            }, commandType: System.Data.CommandType.StoredProcedure);
        }

        private static async Task<string> CopyFileToV2(string file,
            Storage.V2.Services.Grpc.StorageV2.StorageV2Client client,
            Metadata metadata,
            string codigoEmpresa,
            V1.StorageDocument storageDocument)
        {
            var fileInfo = new FileInfo(file);
            var fileDate = storageDocument.DateCreated;
            var containerName = $"DocumentosAutomatizer-{codigoEmpresa}-{fileDate.ToString("yyyy-MM")}";
            var containerResponse = await client.GetContainerAsync(new()
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
                Name = storageDocument.IdArchivoBlob.ToString(),

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
                await storageStream.RequestStream.WriteAsync(new()
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

     
    }
}
