using System;
using System.IO;
using System.Security.Cryptography;

namespace AzureChunkingMediaFileUploader
{
    public static class FileHasher
    {
        public static string MD5Hash(string inputFile)
        {
            // create md5 for the whole file
            string hash;
            using (var fileStream = File.Open(inputFile, FileMode.Open, FileAccess.Read))
            {
                var md5Hash = MD5.Create();
                hash = Convert.ToBase64String(md5Hash.ComputeHash(fileStream));
            }
            return hash;
        }
    }
}