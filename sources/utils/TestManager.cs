using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFTriadBuddy
{
    public class TestManager
    {
#if DEBUG
        public static void RunTests()
        {
            ScreenAnalyzer.EMode testMode = ScreenAnalyzer.EMode.ResetIntermediateData | ScreenAnalyzer.EMode.DebugScreenshotOnly;

            //RunTests("test/auto/cactpot", testMode | ScreenAnalyzer.EMode.ScanCactpot);
            RunTests("test/auto/triad", testMode | ScreenAnalyzer.EMode.ScanTriad);
        }

        public static void RunTests(string path, ScreenAnalyzer.EMode mode)
        {
            ScreenAnalyzer screenAnalyzer = new ScreenAnalyzer();

            ScannerBase scannerOb = null;
            foreach (var kvp in screenAnalyzer.mapScanners)
            {
                if ((kvp.Key & mode) != ScreenAnalyzer.EMode.None)
                {
                    scannerOb = kvp.Value;
                    break;
                }
            }

            if (scannerOb == null)
            {
                throw new Exception("Test failed! Can't find scanner for requested type:" + mode);
            }

            string testRoot = AssetManager.Get().CreateFilePath(path);
            IEnumerable<string> configPaths = Directory.EnumerateFiles(testRoot, "*.json");
            foreach (var configPath in configPaths)
            {
                string imagePath = configPath.Replace(".json", ".jpg");
                if (!File.Exists(imagePath))
                {
                    imagePath = imagePath.Replace(".jpg", ".png");
                }

                if (File.Exists(imagePath))
                {
                    Logger.WriteLine("==> Testing: " + Path.GetFileNameWithoutExtension(configPath));

                    bool bNeedsDebugRun = false;
                    screenAnalyzer.debugScreenshotPath = imagePath;
                    screenAnalyzer.debugScannerContext = null;

                    try
                    {
                        screenAnalyzer.DoWork(mode);
                        scannerOb.ValidateScan(configPath, mode);
                    }
                    catch (Exception)
                    {
                        bNeedsDebugRun = true;
                    }

                    // retry, don't catch exceptions
                    if (bNeedsDebugRun)
                    {
                        screenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.Debug);
                        scannerOb.ValidateScan(configPath, mode | ScreenAnalyzer.EMode.Debug);
                    }
                }
            }
        }
#endif // DEBUG
    }
}
