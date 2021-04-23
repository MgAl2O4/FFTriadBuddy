using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FFTriadBuddy
{
    public class ScreenAnalyzer
    {
        public enum EState
        {
            NoErrors,
            NoInputImage,
            NoScannerMatch,
            UnknownHash,
            ScannerErrors,
        }

        public enum EStateIcon
        {
            None,
            Info,
            Waiting,
            Warning,
            Error,
        }

        [Flags]
        public enum EMode
        {
            None = 0x0,
            Debug = 0x1,
            DebugScreenshotOnly = 0x2,
            DebugSaveMarkup = 0x4,
            AlwaysResetCache = 0x8,
            NeverResetCache = 0x10,            

            ScanTriad = 0x100,
            ScanCactpot = 0x200,

            ScanAll = ScanCactpot | ScanTriad,
            Default = ScanAll,
        }

        public ScreenReader screenReader;
        public ScannerCactpot scannerCactpot;
        public ScannerTriad scannerTriad;

        public Dictionary<EMode, ScannerBase> mapScanners;
        public ScannerBase activeScanner;

        public List<ImageHashData> unknownHashes;
        public List<ImageHashData> currentHashMatches;
        public FastBitmapHSV cachedFastBitmap;
        public Rectangle scanClipBounds;
        public Rectangle currentScanArea;

        public string debugScreenshotPath;
        public string debugScannerContext;

        private EState currentState = EState.NoInputImage;
        private Size cachedBitmapSize;

        public ScreenAnalyzer()
        {
            screenReader = new ScreenReader();
            scannerCactpot = new ScannerCactpot();
            scannerTriad = new ScannerTriad();

            unknownHashes = new List<ImageHashData>();
            currentHashMatches = new List<ImageHashData>();

            mapScanners = new Dictionary<EMode, ScannerBase>();
            mapScanners.Add(EMode.ScanTriad, scannerTriad);
            mapScanners.Add(EMode.ScanCactpot, scannerCactpot);

            foreach (var kvp in mapScanners)
            {
                kvp.Value.Initialize(this);
            }
        }

        public void DoWork(EMode mode = EMode.Default, int scannerFlags = 0)
        {
            Stopwatch timerTotal = new Stopwatch();
            Stopwatch timerStep = new Stopwatch();
            timerTotal.Start();

            bool debugMode = (mode & EMode.Debug) != EMode.None;
            activeScanner = null;

            // load input
            bool hasInputImage = LoadInputImage(mode, timerStep, scanClipBounds);
            if (hasInputImage)
            {
                // convert to HSL representation
                timerStep.Restart();
                cachedFastBitmap = ImageUtils.ConvertToFastBitmap(screenReader.cachedScreenshot);
                timerStep.Stop();
                if (debugMode) { Logger.WriteLine("Screenshot convert: " + timerStep.ElapsedMilliseconds + "ms"); }
                if (Logger.IsSuperVerbose()) { Logger.WriteLine("Screenshot: {0}x{1}", screenReader.cachedScreenshot.Width, screenReader.cachedScreenshot.Height); }

                // reset scanner's intermediate data
                bool canResetCache = (mode & EMode.NeverResetCache) == EMode.None;
                if (canResetCache)
                {
                    bool forceResetCache = (mode & EMode.AlwaysResetCache) != EMode.None;
                    if (forceResetCache)
                    {
                        unknownHashes.Clear();
                        currentHashMatches.Clear();
                    }

                    // invalidate scanner's cache if input image size has changed
                    if (forceResetCache || cachedBitmapSize.Width <= 0 || cachedBitmapSize.Width != cachedFastBitmap.Width || cachedBitmapSize.Height != cachedFastBitmap.Height)
                    {
                        cachedBitmapSize = new Size(cachedFastBitmap.Width, cachedFastBitmap.Height);
                        foreach (var kvp in mapScanners)
                        {
                            kvp.Value.InvalidateCache();
                        }
                    }
                }

                currentState = EState.NoScannerMatch;

                // pass 1: check if cache is still valid for requested scanner
                foreach (var kvp in mapScanners)
                {
                    if ((kvp.Key & mode) != EMode.None && kvp.Value.HasValidCache(cachedFastBitmap, scannerFlags))
                    {
                        bool scanned = false;
                        try
                        {
                            scanned = kvp.Value.DoWork(cachedFastBitmap, scannerFlags, timerStep, debugMode);
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("Failed to scan [1] image! {0}", ex);
                            scanned = false;
                        }

                        if (scanned)
                        {
                            activeScanner = kvp.Value;
                            currentState = (currentState == EState.NoScannerMatch) ? EState.NoErrors : currentState;
                            if (debugMode) Logger.WriteLine("Scan [1] successful, type:{0}, state:{1}", kvp.Key, currentState);
                        }
                        else
                        {
                            currentState = EState.ScannerErrors;
                        }

                        break;
                    }
                }

                // pass 2: all requested
                if (activeScanner == null)
                {
                    foreach (var kvp in mapScanners)
                    {
                        if ((kvp.Key & mode) != EMode.None)
                        {
                            bool scanned = false;
                            try
                            {
                                scanned = kvp.Value.DoWork(cachedFastBitmap, scannerFlags, timerStep, debugMode);
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteLine("Failed to scan [2] image! {0}", ex);
                                scanned = false;
                            }

                            if (scanned)
                            {
                                activeScanner = kvp.Value;
                                currentState = (currentState == EState.NoScannerMatch) ? EState.NoErrors : currentState;
                                if (debugMode) Logger.WriteLine("Scan [2] successful, type:{0}, state:{1}", kvp.Key, currentState);
                                break;
                            }
                        }
                    }
                }

                // save debug markup if needed
                if ((activeScanner != null) && ((mode & EMode.DebugSaveMarkup) != EMode.None))
                {
                    List<Rectangle> debugBounds = new List<Rectangle>();
                    List<ImageUtils.HashPreview> debugHashes = new List<ImageUtils.HashPreview>();
                    activeScanner.AppendDebugShapes(debugBounds, debugHashes);

                    if (currentScanArea.Width > 0) { debugBounds.Add(currentScanArea); }

                    timerStep.Restart();

                    string imagePath = GetDefaultScreenshotPath() + "screenshot-markup.png";
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                    }

                    using (Bitmap markupBitmap = ImageUtils.ConvertToBitmap(cachedFastBitmap))
                    {
                        ImageUtils.DrawDebugShapes(markupBitmap, debugBounds);
                        ImageUtils.DrawDebugHashes(markupBitmap, debugHashes);

                        markupBitmap.Save(imagePath, ImageFormat.Png);
                    }

                    timerStep.Stop();
                    Logger.WriteLine("Screenshot save: " + timerStep.ElapsedMilliseconds + "ms");
                }
            }
            else
            {
                currentState = EState.NoInputImage;
            }

            timerTotal.Stop();
            if (debugMode) { Logger.WriteLine("Screenshot TOTAL: " + timerTotal.ElapsedMilliseconds + "ms"); }
        }

        public EState GetCurrentState() { return currentState; }

        public void OnScannerError()
        {
            if (currentState != EState.UnknownHash)
            {
                currentState = EState.ScannerErrors;
            }
        }

        public void AddImageHash(ImageHashData hashData)
        {
            hashData.sourceImage = screenReader.cachedScreenshot;
            if (hashData.isKnown)
            {
                currentHashMatches.Add(hashData);
            }
            else
            {
                bool alreadyAdded = false;
                foreach (ImageHashData testHash in unknownHashes)
                {
                    if (testHash.type == hashData.type && testHash.ownerOb == hashData.ownerOb)
                    {
                        if (testHash.IsMatching(hashData, 0, out int dummyDistance))
                        {
                            alreadyAdded = true;
                        }
                    }
                }

                Logger.WriteLine("Unknown image hash, type:{0}, new:{1}", hashData.type, !alreadyAdded);
                if (!alreadyAdded)
                {
                    unknownHashes.Add(hashData);
                    currentState = EState.UnknownHash;
                }
            }
        }

        public void PopUnknownHash()
        {
            if (unknownHashes.Count > 0)
            {
                unknownHashes.RemoveAt(0);
            }

            if (unknownHashes.Count == 0)
            {
                currentState = EState.NoErrors;
            }
        }

        public void ClearAll()
        {
            currentHashMatches.Clear();
            unknownHashes.Clear();
            currentScanArea = Rectangle.Empty;
            activeScanner = null;

            currentState = EState.NoErrors;
        }

        public void ClearKnownHashes()
        {
            currentHashMatches.Clear();
        }

        public Rectangle ConvertGameToScreen(Rectangle gameBounds)
        {
            return screenReader.ConvertGameToScreen(gameBounds);
        }

        public void InitializeScreenData()
        {
            screenReader.InitializeScreenData();
            currentState = (screenReader.currentState == ScreenReader.EState.NoErrors) ? EState.NoErrors : EState.NoInputImage;
        }

        private string GetDefaultScreenshotPath()
        {
            string imagePath = AssetManager.Get().CreateFilePath("test/");
            if (!Directory.Exists(imagePath))
            {
                imagePath = AssetManager.Get().CreateFilePath(null);
                if (!Directory.Exists(imagePath))
                {
                    imagePath = Path.GetTempPath();
                }
            }

            return imagePath;
        }

        private bool LoadInputImage(EMode mode, Stopwatch perfTimer, Rectangle optClipBounds)
        {
            string imagePath = GetDefaultScreenshotPath();
            bool debugMode = (mode & EMode.Debug) != EMode.None;
            bool result = false;
            perfTimer.Start();

#if DEBUG
            if (string.IsNullOrEmpty(debugScreenshotPath))
            {
                debugScreenshotPath = imagePath + "screenshot-source-9.jpg";
            }
#endif

            if ((mode & EMode.DebugScreenshotOnly) != EMode.None)
            {
                result = screenReader.LoadScreenshot(debugScreenshotPath, optClipBounds);
            }
            else
            {
                result = screenReader.TakeScreenshot(optClipBounds);
                if (!result)
                {
                    result = screenReader.LoadScreenshot(debugScreenshotPath, optClipBounds);
                }
                else if (debugMode && screenReader.cachedScreenshot != null)
                {
                    for (int idx = 1; idx < 1000000; idx++)
                    {
                        string testPath = imagePath + "screenshot-source-" + idx + ".jpg";
                        if (!File.Exists(testPath))
                        {
                            screenReader.cachedScreenshot.Save(testPath);
                            break;
                        }
                    }
                }
            }

            perfTimer.Stop();
            if (debugMode) 
            {
                string logFile = "";
                if (!string.IsNullOrEmpty(debugScreenshotPath))
                {
                    logFile = " <<= " + Path.GetFileName(debugScreenshotPath);
                }

                Logger.WriteLine("Screenshot load: {0}ms {1}", perfTimer.ElapsedMilliseconds, logFile);
            }

            return result;
        }
    }
}
