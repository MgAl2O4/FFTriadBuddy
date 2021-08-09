using System;
using System.IO;
using System.Reflection;

namespace MgAl2O4.Utils
{
    public class Logger
    {
        private static StreamWriter logWriter;
        private static StreamWriter logWriterDefault;

        private static bool isSuperVerbose = false;

        public static void Initialize(string[] Args)
        {
            foreach (string cmdArg in Args)
            {
                if (cmdArg == "-log")
                {
                    logWriter = new StreamWriter("debugLog.txt");
                }
                else if (cmdArg == "-verbose")
                {
                    isSuperVerbose = true;
                }
            }

            try
            {
                string appName = Assembly.GetEntryAssembly().GetName().Name;
                string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
                Directory.CreateDirectory(settingsPath);

                logWriterDefault = new StreamWriter(Path.Combine(settingsPath, "outputLog.txt"));
            }
            catch (Exception) { }
        }

        public static void Close()
        {
            logWriterDefault.Close();
        }

        public static bool IsActive()
        {
            return logWriter != null;
        }

        public static bool IsSuperVerbose()
        {
            return isSuperVerbose;
        }

        public static void WriteLine(string str)
        {
            if (isSuperVerbose)
            {
                str = DateTime.Now.ToString("hh:mm:ss.fff") + ": " + str;
            }

            Console.WriteLine(str);

            if (logWriter != null)
            {
                logWriter.WriteLine(str);
                logWriter.Flush();
            }

            if (logWriterDefault != null)
            {
                logWriterDefault.WriteLine(str);
            }
        }

        public static void WriteLine(string fmt, params object[] args)
        {
            WriteLine(string.Format(fmt, args));
        }
    }
}
