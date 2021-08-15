using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFTriadBuddy
{
    public class TestManager
    {
#if DEBUG
        private static bool exportDetectionPatterns;

        public static void RunTests()
        {
            // ML run?
            exportDetectionPatterns = false;

            ScreenAnalyzer.EMode testMode = ScreenAnalyzer.EMode.AlwaysResetCache | ScreenAnalyzer.EMode.DebugScreenshotOnly | ScreenAnalyzer.EMode.AutoTest;

            RunTests("test/auto/cactpot", testMode | ScreenAnalyzer.EMode.ScanCactpot);
            RunTests("test/auto/triad", testMode | ScreenAnalyzer.EMode.ScanTriad);
            RunTriadSolverTests("test/auto/triad-solver");
        }

        public static void RunTests(string path, ScreenAnalyzer.EMode mode)
        {
            ScreenAnalyzer screenAnalyzer = new ScreenAnalyzer();
            MLDataExporter dataExporter = null;

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

            if (exportDetectionPatterns)
            {
                dataExporter = new MLDataExporter();
                dataExporter.exportPath = AssetManager.Get().CreateFilePath("ml/patternMatch/data");
                dataExporter.StartDataExport((mode & ScreenAnalyzer.EMode.ScanAll).ToString());
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
                        scannerOb.ValidateScan(configPath, mode, dataExporter);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("Exception:" + ex);
                        bNeedsDebugRun = true;
                    }

                    // retry, don't catch exceptions
                    if (bNeedsDebugRun && !exportDetectionPatterns)
                    {
                        screenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.Debug);
                        scannerOb.ValidateScan(configPath, mode | ScreenAnalyzer.EMode.Debug, null);
                    }
                }
            }

            if (exportDetectionPatterns)
            {
                dataExporter.FinishDataExport("ml-" + Path.GetFileNameWithoutExtension(path) + ".json");
            }
        }

        public static void RunTriadSolverTests(string path)
        {
            string testRoot = AssetManager.Get().CreateFilePath(path);
            IEnumerable<string> configPaths = Directory.EnumerateFiles(testRoot, "*.json");
            foreach (var configPath in configPaths)
            {
                Logger.WriteLine("==> Testing: " + Path.GetFileNameWithoutExtension(configPath));
                bool bNeedsDebugRun = false;

                try
                {
                    TriadGameScreenMemory.RunTest(configPath, bNeedsDebugRun);
                    TriadGameTests.RunTest(configPath, bNeedsDebugRun);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Exception:" + ex);
                    bNeedsDebugRun = true;
                }

                if (bNeedsDebugRun)
                {
                    TriadGameScreenMemory.RunTest(configPath, bNeedsDebugRun);
                    TriadGameTests.RunTest(configPath, bNeedsDebugRun);
                }
            }
        }
#endif // DEBUG
    }
}
