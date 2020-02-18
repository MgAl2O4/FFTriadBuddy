using System;
using System.IO;

namespace FFTriadBuddy
{
    public class Logger
    {
        private static StreamWriter logWriter;
        private static string outputDir;

        public static void Initialize(string[] Args)
        {
            outputDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            outputDir = Path.Combine(outputDir, "FFTriadBuddy");

            try
            {
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                string logPath = Path.Combine(outputDir, "outputLog.txt");
                logWriter = new StreamWriter(logPath);
            }
            catch (Exception)
            {
                logWriter = null;
                outputDir = null;
            }
        }

        public static bool IsActive()
        {
            return logWriter != null;
        }

        public static void WriteLine(string str)
        {
            Console.WriteLine(str);
            if (logWriter != null)
            {
                logWriter.WriteLine(str);
                logWriter.Flush();
            }
        }

        public static string GetOutputDir()
        {
            return outputDir;
        }
    }
}
