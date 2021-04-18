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
            ResetIntermediateData = 0x4,

            ScanTriad = 0x100,
            ScanCactpot = 0x200,

            ScanAll = ScanCactpot | ScanTriad,
            Default = ResetIntermediateData | ScanAll,
        }

        public ScreenReader screenReader;
        public ScannerCactpot scannerCactpot;
        public ScannerTriad scannerTriad;

        public Dictionary<EMode, ScannerBase> mapScanners;
        public ScannerBase activeScanner;

        public List<ImageHashUnknown> unknownHashes;
        public Dictionary<FastBitmapHash, int> currentHashDetections;
        public Rectangle scanClipBounds;
        public Rectangle currentScanArea;
        
        public string debugScreenshotPath;
        public string debugScannerContext;

        private EState currentState = EState.NoInputImage;
        private Size currentImageSize;

        public ScreenAnalyzer()
        {
            screenReader = new ScreenReader();
            scannerCactpot = new ScannerCactpot();
            scannerTriad = new ScannerTriad();

            unknownHashes = new List<ImageHashUnknown>();
            currentHashDetections = new Dictionary<FastBitmapHash, int>();

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
                FastBitmapHSV bitmap = ImageUtils.ConvertToFastBitmap(screenReader.cachedScreenshot);
                timerStep.Stop();
                if (debugMode) { Logger.WriteLine("Screenshot convert: " + timerStep.ElapsedMilliseconds + "ms"); }
                if (Logger.IsSuperVerbose()) { Logger.WriteLine("Screenshot: {0}x{1}", screenReader.cachedScreenshot.Width, screenReader.cachedScreenshot.Height); }

                // reset scanner's intermediate data
                if ((mode & EMode.ResetIntermediateData) != EMode.None)
                {
                    unknownHashes.Clear();
                    currentHashDetections.Clear();

                    // invalidate scanner's cache if input image size has changed
                    if (currentImageSize.Width <= 0 || currentImageSize.Width != bitmap.Width || currentImageSize.Height != bitmap.Height)
                    {
                        currentImageSize = new Size(bitmap.Width, bitmap.Height);
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
                    if ((kvp.Key & mode) != EMode.None && kvp.Value.HasValidCache(bitmap, scannerFlags))
                    {
                        bool scanned = false;
                        try
                        {
                            scanned = kvp.Value.DoWork(bitmap, scannerFlags, timerStep, debugMode);
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
                                scanned = kvp.Value.DoWork(bitmap, scannerFlags, timerStep, debugMode);
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
                if (debugMode && activeScanner != null)
                {
                    List<Rectangle> debugBounds = new List<Rectangle>();
                    activeScanner.AppendDebugShapes(debugBounds);

                    if (currentScanArea.Width > 0) { debugBounds.Add(currentScanArea); }

                    timerStep.Restart();

                    string imagePath = GetDefaultScreenshotPath() + "screenshot-markup.png";
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                    }

                    using (Bitmap bmp = ImageUtils.CreateBitmapWithShapes(bitmap, debugBounds, activeScanner.debugHashes))
                    {
                        bmp.Save(imagePath, ImageFormat.Png);
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

        public void OnUnknownHashAdded()
        {
            currentState = EState.UnknownHash;
        }

        public void OnScannerError()
        {
            if (currentState != EState.UnknownHash)
            {
                currentState = EState.ScannerErrors;
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

        public void RemoveKnownHash(FastBitmapHash hashToRemove)
        {
            PlayerSettingsDB.Get().AddLockedHash(new ImageHashData(hashToRemove.GuideOb, hashToRemove.Hash, EImageHashType.None));
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
                debugScreenshotPath = imagePath + "screenshot-source-4.jpg";
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
            if (debugMode) { Logger.WriteLine("Screenshot load: " + perfTimer.ElapsedMilliseconds + "ms"); }

            return result;
        }
    }
}
