using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientStudentVer
{
    [Serializable]
    public class AesKey
    {
        public byte[] AesKey_keyEncrypted { get; set; }
        public byte[] AesKey_LenK { get; set; }
        public byte[] AesKey_LenIV { get; set; }
        public int AesKey_lKey { get; set; }
        public int AesKey_lIV { get; set; }
        public byte[] AesKey_IV { get; set; }
    }
}
