using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace Wave
{
    public static class Embedded
    {
        public static void WriteEmbeddedFile(string embeddedPath, string localPath, string asmbName)
        {
            Assembly asmb = Assembly.Load(asmbName);
            using(Stream inputStream = asmb.GetManifestResourceStream(embeddedPath))
            using(FileStream outputStream=new FileStream(localPath,FileMode.OpenOrCreate,FileAccess.Write))
            {
                byte[] bytes = new byte[inputStream.Length];
                inputStream.Read(bytes, 0, bytes.Length);
                outputStream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
