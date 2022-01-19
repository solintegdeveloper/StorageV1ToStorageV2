using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageV1ToStorageV2
{
    public class Options
    {
        [Option('s', Default ="Localhost", HelpText = "Servidor al que conectarse", Required =true)]
        public string? Server { get; set; }
        
    }
}
