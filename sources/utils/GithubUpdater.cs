using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MgAl2O4.Utils
{
    public class GithubUpdater
    {
        const string UpdateFileNamePart = "temp-update-v";
        const string repoLink = "https://github.com/MgAl2O4/FFTriadBuddy/";

        public static bool FindAndApplyUpdates()
        {
            string updateFilePath = FindPendingUpdateFile();
            bool needsUpdate = !string.IsNullOrEmpty(updateFilePath);
            if (needsUpdate)
            {
                ApplyUpdate(updateFilePath);
            }

            return needsUpdate;
        }

        public static bool FindAndDownloadUpdates(out string statusMsg)
        {
            bool bFoundUpdate = false;
            try
            {
                Version version = Assembly.GetEntryAssembly().GetName().Version;
                int currentVersion = version.Major;
                int onlineVersion = FindOnlineVersion(out string downloadLink);

                if (onlineVersion > currentVersion)
                {
                    DownloadUpdate(onlineVersion, downloadLink);
                    statusMsg = "downloaded update file, version: " + onlineVersion;
                    bFoundUpdate = true;
                }
                else
                {
                    statusMsg = "program is up to date, online version: " + onlineVersion;
                }
            }
            catch (Exception ex)
            {
                statusMsg = "failed! " + ex;
            }

            return bFoundUpdate;
        }

        private static int FindOnlineVersion(out string downloadLink)
        {
            int latestVersion = 0;
            downloadLink = "";

            WebRequest ReqTree = WebRequest.Create(repoLink + "releases/latest/");
            ReqTree.Timeout = -1;

            WebResponse RespTree = ReqTree.GetResponse();
            using (Stream dataStream = RespTree.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();

                string pattern = "href=\\\".+\\/(releases\\/download\\/.+v(\\d+)\\.zip)\\\"";
                Match match = new Regex(pattern).Match(responseFromServer);
                if (match.Success)
                {
                    downloadLink = match.Groups[1].Value;

                    string versionStr = match.Groups[2].Value;
                    latestVersion = int.Parse(versionStr);
                }
            }

            return latestVersion;
        }

        private static void DownloadUpdate(int version, string downloadLink)
        {
            string filePath = UpdateFileNamePart + version + ".zip";

            WebRequest ReqUpdate = WebRequest.Create(repoLink + downloadLink);
            WebResponse RespUpdate = ReqUpdate.GetResponse();
            using (Stream dataStream = RespUpdate.GetResponseStream())
            {
                FileStream outputFile = new FileStream(filePath, FileMode.Create);
                dataStream.CopyTo(outputFile);
                outputFile.Close();
            }
        }

        private static void ApplyUpdate(string updateFilePath)
        {
            List<string> updateBatchLines = new List<string>();

            string updateExecutableName = "";
            ZipArchive zipArchive = ZipFile.OpenRead(updateFilePath);
            foreach (ZipArchiveEntry entry in zipArchive.Entries)
            {
                if (entry.FullName.EndsWith(".exe"))
                {
                    updateExecutableName = entry.FullName;
                }
            }

            updateBatchLines.Add("@echo off");
            updateBatchLines.Add("echo Waiting for program to finish...");
            updateBatchLines.Add(":loop");
            updateBatchLines.Add("tasklist | find /i \"" + updateExecutableName + "\" >nul 2>&1");
            updateBatchLines.Add("if errorlevel 1 ( goto update ) else (");
            updateBatchLines.Add("  timeout /T 1 /Nobreak");
            updateBatchLines.Add("  goto loop");
            updateBatchLines.Add(")");
            updateBatchLines.Add(":update");
            updateBatchLines.Add("echo Updating...");

            foreach (ZipArchiveEntry entry in zipArchive.Entries)
            {
                string newFileName = entry.FullName + ".new";
                updateBatchLines.Add("move /y " + newFileName + " " + entry.FullName);

                if (File.Exists(newFileName)) { File.Delete(newFileName); }
                entry.ExtractToFile(newFileName);
            }

            zipArchive.Dispose();
            File.Delete(updateFilePath);

            string updateBatchFile = updateFilePath.Replace(".zip", ".bat");
            updateBatchLines.Add("start " + updateExecutableName);
            updateBatchLines.Add("del /q " + updateBatchFile);

            File.WriteAllLines(updateBatchFile, updateBatchLines);

            ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", "/c " + updateBatchFile)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(processInfo);
        }

        private static string FindPendingUpdateFile()
        {
            string updatePath = null;

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            int updateVersion = version.Major;
            Logger.WriteLine("Update version check! current:" + updateVersion);

            try
            {
                string[] files = Directory.GetFiles(".", UpdateFileNamePart + "*.zip");
                foreach (string path in files)
                {
                    string versionStr = Path.GetFileNameWithoutExtension(path).Substring(UpdateFileNamePart.Length);
                    if (!string.IsNullOrEmpty(versionStr))
                    {
                        int versionNum = int.Parse(versionStr);
                        if (versionNum > updateVersion)
                        {
                            Logger.WriteLine(">> found '" + path + "', version: " + versionNum);
                            updateVersion = versionNum;
                            updatePath = path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Update failed: " + ex);
            }

            return updatePath;
        }
    }
}
