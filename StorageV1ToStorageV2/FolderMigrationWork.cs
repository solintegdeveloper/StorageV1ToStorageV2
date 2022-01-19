using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public class FolderMigrationWork
    {
        public V1.StorageFolder Folder { get; set; }
        public List<V1.StorageDocument> Documents { get; set; }
        public FolderMigrationWork(V1.StorageFolder folder, List<V1.StorageDocument> documents )
        {
            Folder = folder;
            Documents = documents;
            
        }

    }
}
