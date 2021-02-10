using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DHIS2Xfer.Factory
{
    public class FileFactory
    {
        public static string ValidateDirectory(string directory,XferFactory.FileType type)
        {
            string dir = "";

            //Check whether to use the default base directory
            if (directory == "Default" || directory == "")
                dir = AppDomain.CurrentDomain.BaseDirectory + "Files\\" + XferFactory.GetTypeFolder(type);
            else
            {
                dir = directory;
                if (dir.Substring(dir.Length - 1) != "\\")
                    dir += "\\";

                dir += XferFactory.GetTypeFolder(type);
            }

            //Check if the directory exists, else create it
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }

        public static void WriteFile(JObject data, string name,XferFactory.FileType type,string directory)
        {
            string dir = ValidateDirectory(directory,type);

            string encrypt = SecurityFactory.Encrypt(data.ToString(), SecurityFactory.SecurityKey);

            using (StreamWriter file = new StreamWriter(dir + "\\" + name + XferFactory.GetTypeExtension(type)))
            {
                file.Write(encrypt);
            }
        }

        public static JObject ReadFile(string path)
        {
            string result = "";

            if (!File.Exists(path))
                return null;

            using (StreamReader file = new StreamReader(path))
            {
                result = file.ReadToEnd();
            }

            string decrypt = SecurityFactory.Decrypt(result, SecurityFactory.SecurityKey);

            JObject data = JObject.Parse(decrypt);

            return data;
        }

        public static JObject GetFile(string id, XferFactory.FileType type, string directory)
        {
            string dir = ValidateDirectory(directory, type);

            string path = dir + "\\" + id + XferFactory.GetTypeExtension(type);
            JObject file = ReadFile(path);

            return file;
        }

        public static List<string> GetFileList(XferFactory.FileType type, string directory)
        {
            string dir = ValidateDirectory(directory, type);

            List<string> files =  Directory.GetFiles(dir).ToList();

            return files;
        }

        public static void RemoveFile(string id, XferFactory.FileType type, string directory)
        {
            string dir = ValidateDirectory(directory, type);

            string path = dir + "\\" + id + XferFactory.GetTypeExtension(type);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

        }
    }
}
