using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public  record struct MigrationProcessorConfiguration(
        string Server, 
        string DbServer,
        string DbBlobsV1,
        string DbAutomatizer, 
        string CodigoEmpresa, 
        string FiletableRootPath);
    
}
