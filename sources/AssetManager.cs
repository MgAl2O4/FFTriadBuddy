using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FFTriadBuddy
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

        public bool Init()
        {
            bool bResult = false;
            try
            {
                byte[] zipContent = Properties.Resources.assets;
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

            AssemblyTitleAttribute attributes = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false);
            return Path.Combine(currentDirName, relativeFilePath);
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
    }
}
