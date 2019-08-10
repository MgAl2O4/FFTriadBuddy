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
        public string DBRelativePath;
        private string DBPath;
        private ZipArchive assetArchive;

        private static AssetManager instance = new AssetManager();

        public AssetManager()
        {
            AssemblyTitleAttribute attributes = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false);
            DBRelativePath = attributes.Title + ".pkg";
            DBPath = CreateFilePath(DBRelativePath);
        }

        public static AssetManager Get()
        {
            return instance;
        }

        public bool Init()
        {
            bool bResult = false;
            if (File.Exists(DBPath))
            {
                try
                {
                    assetArchive = ZipFile.OpenRead(DBPath);
                    bResult = true;
                }
                catch (Exception)
                {
                }
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
            string[] devIgnorePatterns = new string[] { @"bin\Debug", @"bin\Release" };
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
            foreach (ZipArchiveEntry entry in assetArchive.Entries)
            {
                if (entry.FullName.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    return entry.Open();
                }
            }

            return null;
        }
    }
}
