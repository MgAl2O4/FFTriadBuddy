using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MgAl2O4.Utils
{
    public class AssetManager
    {
        private ZipArchive assetArchive;
        private Stream resourceReader;

        private static AssetManager instance = new AssetManager();

        public static AssetManager Get()
        {
            return instance;
        }

        public bool Init(byte[] zipContent)
        {
            bool bResult = false;
            try
            {
                resourceReader = new MemoryStream(zipContent);
                assetArchive = new ZipArchive(resourceReader);
                bResult = assetArchive.Entries.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Can't access embedded assets! " + ex);
            }

            return bResult;
        }

        public void Close()
        {
            assetArchive.Dispose();
            assetArchive = null;
        }

        public string CreateFilePath(string relativeFilePath)
        {
            string currentDirName = Environment.CurrentDirectory;
            string[] devIgnorePatterns = new string[] { @"sources\bin\Debug", @"sources\bin\Release" };
            foreach (string pattern in devIgnorePatterns)
            {
                if (currentDirName.EndsWith(pattern))
                {
                    currentDirName = currentDirName.Remove(currentDirName.Length - pattern.Length);
                    break;
                }
            }

            return string.IsNullOrEmpty(relativeFilePath) ? currentDirName : Path.Combine(currentDirName, relativeFilePath);
        }

        public Stream GetAsset(string path)
        {
            path = path.Replace("/", "\\");
            foreach (ZipArchiveEntry entry in assetArchive.Entries)
            {
                string compareName = entry.FullName.Replace("/", "\\");

                if (compareName.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    return entry.Open();
                }
            }

            return null;
        }

        public List<string> ListAssets()
        {
            List<string> assetPaths = new List<string>();
            foreach (ZipArchiveEntry entry in assetArchive.Entries)
            {
                assetPaths.Add(entry.FullName);
            }

            return assetPaths;
        }
    }
}
