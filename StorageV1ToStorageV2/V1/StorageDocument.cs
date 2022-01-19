using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2.V1
{
    public class StorageDocument
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public byte[] Thumbnail { get; set; }
        public bool Private { get; set; }
        public bool ReadOnly { get; set; }
        public string UserOwner { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public byte TypeDoc { get; set; }
        public byte Origin { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public string Hash { get; set; }
        public bool IsPasswordProtected { get; set; }
        public long IdParent { get; set; }
        public long IdRootParent { get; set; }
        public string FullPath { get; set; }
        public string FullIdsPath { get; set; }
        public Guid IdArchivoBlob { get; set; }
    }
}
