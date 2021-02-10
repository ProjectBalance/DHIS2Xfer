using DHIS2Xfer.Factory;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XferCore
{
    class FileFactory
    {
        public static JObject ReadFile(string path)
        {
            string result = "";

            if (!File.Exists(path))
                return null;

            using (StreamReader file = new StreamReader(path))
            {
                result = file.ReadToEnd();
            }

            string decrypt = CryptoFactory.Decrypt(result, CryptoFactory.SecurityKey);

            JObject data = JObject.Parse(decrypt);

            return data;
        }

        public static JObject GetFile(string id, string directory)
        {
            string path = directory + "\\" + id + ".xfer";
            JObject file = ReadFile(path);

            return file;
        }

        public static List<string> GetFileList(string directory)
        {

            List<string> files = Directory.GetFiles(directory).ToList();

            return files;
        }
    }
}
