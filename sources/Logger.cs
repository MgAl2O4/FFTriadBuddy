using System;
using System.IO;

namespace FFTriadBuddy
{
    public class Logger
    {
        private static StreamWriter logWriter;

        public static void Initialize(string[] Args)
        {
            foreach (string cmdArg in Args)
            {
                if (cmdArg == "-log")
                {
                    logWriter = new StreamWriter("debugLog.txt");
                }
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
    }
}
