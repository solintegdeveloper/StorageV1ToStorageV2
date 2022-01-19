using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2.V1
{
    public class StorageFolder
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long IdParent { get; set; }
        public bool IsRoot { get; set; }
        public byte[] FolderIcon { get; set; }
        public string EmpCod { get; set; }
        public byte ModIde { get; set; }
        public string TrnCod { get; set; }
        public int TrnNum { get; set; }
        public DateTime FechaCreacion { get; set; }
        public long IdRootParent { get; set; }
        public string FullPath { get; set; }
        public string FullIdsPath { get; set; }
    }
}
