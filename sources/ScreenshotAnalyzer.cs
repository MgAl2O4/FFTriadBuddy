using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace FFTriadBuddy
{
    public class ScreenshotAnalyzer
    {
        [Flags]
        public enum EMode
        {
            None = 0,
            Decks = 1,
            Rules = 2,
            Board = 4,
            Debug = 8,
            DebugForceCached = 16,
            DebugTimerDetails = 32,
            TurnTimerOnly = 64,
            All = Decks | Rules | Board,
        }

        [Flags]
        enum ESide
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8,
        }

        public enum EState
        {
            NoErrors,
            MissingGameProcess,
            MissingGameWindow,
            MissingGrid,
            MissingCards,
            FailedCardMatching,
            UnknownHash,
        }

        public enum ETurnState
        {
            MissingTimer,
            Waiting,
            Active,
        }

        public class GameState
        {
            public TriadCard[] board;
            public ETriadCardOwner[] boardOwner;
            public TriadCard[] blueDeck;
            public TriadCard[] redDeck;
            public TriadCard forcedBlueCard;
            public List<TriadGameModifier> mods;

            public GameState()
            {
                board = new TriadCard[9];
                boardOwner = new ETriadCardOwner[9];
                blueDeck = new TriadCard[5];
                redDeck = new TriadCard[5];
                forcedBlueCard = null;
                mods = new List<TriadGameModifier>();
            }
        }

        public class CardState : IComparable<CardState>
        {
            public TriadCard card;
            public string name;
            public int[] sideNumber;
            public int[] adjustNumber;
            public ImageDataDigit[] sideImage;
            public bool failedMatching;

            public int CompareTo(CardState other)
            {
                if (failedMatching == other.failedMatching)
                {
                    return name.CompareTo(other.name);
                }

                return failedMatching ? -1 : 1;
            }
        }

        private FastPixelMatch colorMatchGridBorder = new FastPixelMatchMono(0, 150);
        private FastPixelMatch colorMatchGridField = new FastPixelMatchMono(170, 255);
        private FastPixelMatch colorMatchRuleBox = new FastPixelMatchMono(20, 80);
        private FastPixelMatch colorMatchDeckBack = new FastPixelMatchHSV(275, 325, 0, 100, 0, 100);
        private FastPixelMatch colorMatchRuleText = new FastPixelMatchMono(150, 255);
        private FastPixelMatch colorMatchCardBorder = new FastPixelMatchHSV(20, 40, 0, 100, 0, 100);
        private FastPixelMatch colorMatchCardNumber = new FastPixelMatchMono(220, 255);
        private FastPixelMatch colorMatchCardOwnerRed1 = new FastPixelMatchHSV(0, 10, 30, 60, 20, 60);
        private FastPixelMatch colorMatchCardOwnerRed2 = new FastPixelMatchHSV(350, 360, 30, 60, 20, 60);
        private FastPixelMatch colorMatchCardOwnerBlue = new FastPixelMatchHSV(190, 260, 20, 60, 20, 60);
        private FastPixelMatch colorMatchTimerBox = new FastPixelMatchHSV(40, 60, 10, 40, 0, 100);
        private FastPixelMatch colorMatchTimerActive = new FastPixelMatchMono(80, 255);

        private Process cachedProcess;
        private Rectangle cachedGameWindow;
        private Bitmap cachedScreenshot;
        private Bitmap debugScreenshot;
        private Rectangle cachedGridCoord;
        private Rectangle cachedScanAreaBox;
        private Rectangle cachedRuleBox;
        private Rectangle cachedTimerBox;
        private Rectangle cachedTimerScanBox;
        private Rectangle[] cachedBlueCards;
        private Rectangle[] cachedRedCards;
        private Rectangle[] cachedBoardCards;
        private bool bHasCachedData = false;
        private bool bDebugMode = false;
        private List<ImagePatternDigit> digitMasks = new List<ImagePatternDigit>();
        private List<FastBitmapHash> debugHashes = new List<FastBitmapHash>();
        private List<Rectangle> debugShapes = new List<Rectangle>();
        private TriadCard failedMatchCard = new TriadCard(-1, "failedMatch", "", ETriadCardRarity.Common, ETriadCardType.None, 0, 0, 0, 0, 0);

        private EState currentState = EState.NoErrors;
        private ETurnState currentTurnState = ETurnState.MissingTimer;
        public GameState currentGame = new GameState();
        public Dictionary<FastBitmapHash, int> currentHashDetections = new Dictionary<FastBitmapHash, int>();
        public List<ImageHashUnknown> unknownHashes = new List<ImageHashUnknown>();
        public List<CardState> currentCardState = new List<CardState>();

        public ScreenshotAnalyzer()
        {
            digitMasks.Add(new ImagePatternDigit(1, new byte[] { 0x00, 0x00, 0x00, 0x02, 0x02, 0x03, 0xff, 0x00, 0x00, 0x00 }));
            digitMasks.Add(new ImagePatternDigit(2, new byte[] { 0xc2, 0xa1, 0x91, 0x91, 0x91, 0x89, 0x89, 0x89, 0x8e, 0x80 }));
            digitMasks.Add(new ImagePatternDigit(3, new byte[] { 0x22, 0x43, 0x81, 0x81, 0x89, 0x89, 0x89, 0x89, 0x5f, 0x76 }));
            digitMasks.Add(new ImagePatternDigit(4, new byte[] { 0x20, 0x30, 0x28, 0x24, 0x20, 0x20, 0x21, 0xff, 0x20, 0x20 }));
            digitMasks.Add(new ImagePatternDigit(5, new byte[] { 0x4e, 0x09, 0x89, 0x89, 0x89, 0x89, 0x89, 0x09, 0x79, 0x00 }));
            digitMasks.Add(new ImagePatternDigit(6, new byte[] { 0x7e, 0x09, 0x89, 0x89, 0x89, 0x89, 0x89, 0x09, 0x79, 0x00 }));
            digitMasks.Add(new ImagePatternDigit(7, new byte[] { 0x01, 0x01, 0x01, 0xc1, 0x31, 0x09, 0x05, 0x03, 0x03, 0x00 }));
            digitMasks.Add(new ImagePatternDigit(8, new byte[] { 0x76, 0x09, 0x89, 0x89, 0x89, 0x89, 0x89, 0x89, 0x5f, 0x00 }));
            digitMasks.Add(new ImagePatternDigit(9, new byte[] { 0x06, 0x51, 0x91, 0x91, 0x91, 0x91, 0x91, 0x91, 0x7f, 0x3e }));
            digitMasks.Add(new ImagePatternDigit(10, new byte[] { 0xe0, 0x70, 0x3c, 0x26, 0x23, 0x21, 0x23, 0x2e, 0x38, 0x60 }));
        }

        public void DoWork(EMode mode)
        {
            if ((mode & EMode.TurnTimerOnly) != EMode.None && cachedTimerScanBox.Width <= 0)
            {
                currentTurnState = ETurnState.MissingTimer;
                return;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool bTurnTimerOnly = (mode & EMode.TurnTimerOnly) != EMode.None;
            string imagePath = AssetManager.Get().CreateFilePath("test/" + (bTurnTimerOnly ? "timer-" : ""));
            bool bUseTestScreenshot = false;
            bDebugMode = (mode & EMode.Debug) != EMode.None;

            Stopwatch stopwatchInner = new Stopwatch();
            stopwatchInner.Start();

            HandleRef windowHandle = FindGameWindow();
            bool bHasWindow = windowHandle.Handle.ToInt64() != 0;
            bUseTestScreenshot = (!bHasWindow && bDebugMode) || ((mode & EMode.DebugForceCached) != EMode.None);

            if (bUseTestScreenshot)
            {
                if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                if (bTurnTimerOnly)
                {
                    cachedScreenshot = LoadTestScreenshot(imagePath + "test.jpg");
                }
                else
                {
                    cachedScreenshot = LoadTestScreenshot(imagePath + "test-noRules.jpg");
                    cachedGameWindow = (cachedScreenshot != null) ? new Rectangle(0, 0, cachedScreenshot.Width, cachedScreenshot.Height) : new Rectangle();
                }
            }
            else
            {
                if (!bHasWindow)
                {
                    return;
                }

                if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                cachedScreenshot = bTurnTimerOnly ? TakeScreenshotPartial(windowHandle, cachedTimerScanBox) : TakeScreenshot(windowHandle);

                if (cachedScreenshot != null && bDebugMode)
                {
                    cachedScreenshot.Save(imagePath + "screenshot-source.jpg");
                }
            }

            stopwatchInner.Stop();
            if (bDebugMode) { Logger.WriteLine("Screenshot load: " + stopwatchInner.ElapsedMilliseconds + "ms"); }

            if (cachedScreenshot != null)
            {
                stopwatchInner.Restart();
                FastBitmapHSV fastBitmap = ScreenshotUtilities.ConvertToFastBitmap(cachedScreenshot);
                stopwatchInner.Stop();
                if (bDebugMode) { Logger.WriteLine("Screenshot convert: " + stopwatchInner.ElapsedMilliseconds + "ms"); }

                debugShapes.Clear();

                if (bTurnTimerOnly)
                {
                    Rectangle fastTimerBox = FindTimerBox(fastBitmap, cachedTimerScanBox);
                    if (fastTimerBox.Width > 0)
                    {
                        debugShapes.Add(fastTimerBox);

                        bool bHasActiveTimer = ParseTimer(fastBitmap, fastTimerBox);
                        currentTurnState = bHasActiveTimer ? ETurnState.Active : ETurnState.Waiting;
                    }
                    else
                    {
                        currentTurnState = ETurnState.MissingTimer;
                    }

                    if ((mode & EMode.DebugTimerDetails) != EMode.None)
                    {
                        debugScreenshot = ScreenshotUtilities.CreateBitmapWithShapes(fastBitmap, debugShapes, new List<FastBitmapHash>());
                    }

                    // debug markup
                    if (bDebugMode)
                    {
                        stopwatchInner.Restart();
                        ScreenshotUtilities.SaveBitmapWithShapes(fastBitmap, debugShapes, new List<FastBitmapHash>(), imagePath + "screenshot-markup.png");
                        stopwatchInner.Stop();
                        if (bDebugMode) { Logger.WriteLine("Screenshot save: " + stopwatchInner.ElapsedMilliseconds + "ms"); }
                    }
                }
                else
                {
                    // update cached data if needed
                    bool bFoundCachedData = false;
                    if (!bHasCachedData ||
                        cachedGridCoord.Width <= 0 ||
                        !HasGridMatch(fastBitmap, cachedGridCoord.Left, cachedGridCoord.Top, cachedGridCoord.Width / 3))
                    {
                        cachedBlueCards = null;
                        cachedRedCards = null;
                        cachedBoardCards = null;

                        cachedGridCoord = FindGridCoords(fastBitmap);
                        if (cachedGridCoord.Width > 0)
                        {
                            cachedRuleBox = FindRuleBoxCoords(fastBitmap, cachedGridCoord);
                            cachedTimerBox = FindTimerBox(fastBitmap, cachedGridCoord, cachedRuleBox);
                            cachedTimerScanBox = new Rectangle(cachedTimerBox.X, cachedTimerBox.Y - 20, cachedTimerBox.Width, cachedTimerBox.Height + 40);
                            cachedScanAreaBox = new Rectangle(cachedGridCoord.Left - (cachedGridCoord.Width * 85 / 100), 
                                cachedGridCoord.Top - (cachedGridCoord.Height * 5 /100), 
                                cachedGridCoord.Width * 270 / 100, 
                                cachedGridCoord.Height * 110 / 100);

                            cachedBlueCards = FindBlueCardCoords(fastBitmap, cachedGridCoord);
                            if (cachedBlueCards != null && cachedBlueCards.Length == 5)
                            {
                                cachedRedCards = FindRedCardCoords(cachedGridCoord, cachedBlueCards);
                                cachedBoardCards = FindBoardCardCoords(cachedGridCoord, cachedBlueCards);
                                bFoundCachedData = true;
                            }
                            else
                            {
                                currentState = EState.MissingCards;
                            }
                        }
                        else
                        {
                            currentState = EState.MissingGrid;
                        }
                    }
                    else
                    {
                        bFoundCachedData = true;
                    }

                    debugHashes.Clear();
                    unknownHashes.Clear();
                    currentHashDetections.Clear();

                    if (bFoundCachedData)
                    {
                        // analyze cards and rules
                        if ((mode & EMode.Rules) != EMode.None)
                        {
                            ParseRules(fastBitmap, cachedRuleBox, currentGame.mods);
                        }

                        bool bCanContinue = currentState != EState.UnknownHash;
                        if (bCanContinue)
                        {
                            bool bHasFailedCardMatch = false;

                            currentState = EState.NoErrors;
                            currentCardState.Clear();

                            List<ImagePatternDigit> listDigits = new List<ImagePatternDigit>();
                            listDigits.AddRange(digitMasks);
                            listDigits.AddRange(PlayerSettingsDB.Get().customDigits);

                            if ((mode & EMode.Decks) != EMode.None)
                            {
                                stopwatchInner.Restart();

                                bool[] greyedOutBlue = new bool[5];
                                int numGreyedOutBlue = 0;
                                for (int Idx = 0; Idx < 5; Idx++)
                                {
                                    currentGame.blueDeck[Idx] = ParseCard(fastBitmap, cachedBlueCards[Idx], listDigits, "blue" + Idx, out greyedOutBlue[Idx]);
                                    currentGame.redDeck[Idx] = ParseCard(fastBitmap, cachedRedCards[Idx], listDigits, "red" + Idx, out bool dummyFlag);
                                    numGreyedOutBlue += greyedOutBlue[Idx] ? 1 : 0;
                                    bHasFailedCardMatch = bHasFailedCardMatch || (currentGame.blueDeck[Idx] == failedMatchCard) || (currentGame.redDeck[Idx] == failedMatchCard);
                                }

                                currentGame.forcedBlueCard = null;
                                if (numGreyedOutBlue > 0)
                                {
                                    for (int Idx = 0; Idx < 5; Idx++)
                                    {
                                        if (currentGame.blueDeck[Idx] != null && !greyedOutBlue[Idx])
                                        {
                                            currentGame.forcedBlueCard = currentGame.blueDeck[Idx];
                                        }
                                    }
                                }

                                stopwatchInner.Stop();
                                if (bDebugMode) { Logger.WriteLine("Parse decks: " + stopwatchInner.ElapsedMilliseconds + "ms"); }
                            }

                            if ((mode & EMode.Board) != EMode.None)
                            {
                                stopwatchInner.Restart();

                                for (int Idx = 0; Idx < 9; Idx++)
                                {
                                    currentGame.board[Idx] = ParseCard(fastBitmap, cachedBoardCards[Idx], listDigits, "board" + Idx, out bool dummyFlag);
                                    bHasFailedCardMatch = bHasFailedCardMatch || (currentGame.board[Idx] == failedMatchCard);

                                    if (currentGame.board[Idx] != null)
                                    {
                                        currentGame.boardOwner[Idx] = ParseCardOwner(fastBitmap, cachedBoardCards[Idx], "board" + Idx);
                                    }
                                }

                                stopwatchInner.Stop();
                                if (bDebugMode) { Logger.WriteLine("Parse board: " + stopwatchInner.ElapsedMilliseconds + "ms"); }
                            }

                            if (bHasFailedCardMatch)
                            {
                                if (currentState != EState.FailedCardMatching && Logger.IsActive() && !bDebugMode)
                                {
                                    string screenshotName = "screenshot_failed_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                    cachedScreenshot.Save(imagePath + screenshotName + ".jpg");

                                    List<Rectangle> debugBounds = new List<Rectangle>();
                                    if (cachedGridCoord.Width > 0) { debugBounds.Add(cachedGridCoord); }
                                    if (cachedRuleBox.Width > 0) { debugBounds.Add(cachedRuleBox); }
                                    if (cachedTimerBox.Width > 0) { debugBounds.Add(cachedTimerBox); }
                                    if (cachedBlueCards != null) { debugBounds.AddRange(cachedBlueCards); }
                                    if (cachedRedCards != null) { debugBounds.AddRange(cachedRedCards); }
                                    if (cachedBoardCards != null) { debugBounds.AddRange(cachedBoardCards); }
                                    debugBounds.AddRange(debugShapes);

                                    ScreenshotUtilities.SaveBitmapWithShapes(fastBitmap, debugBounds, debugHashes, imagePath + screenshotName + "-markup.png");
                                }

                                currentState = EState.FailedCardMatching;
                            }
                        }
                    }

                    // debug markup
                    if (bDebugMode)
                    {
                        List<Rectangle> debugBounds = new List<Rectangle>();
                        if (cachedScanAreaBox.Width > 0) { debugBounds.Add(cachedScanAreaBox); }
                        if (cachedGridCoord.Width > 0) { debugBounds.Add(cachedGridCoord); }
                        if (cachedRuleBox.Width > 0) { debugBounds.Add(cachedRuleBox); }
                        if (cachedTimerBox.Width > 0) { debugBounds.Add(cachedTimerBox); }
                        if (cachedBlueCards != null) { debugBounds.AddRange(cachedBlueCards); }
                        if (cachedRedCards != null) { debugBounds.AddRange(cachedRedCards); }
                        if (cachedBoardCards != null) { debugBounds.AddRange(cachedBoardCards); }
                        debugBounds.AddRange(debugShapes);

                        stopwatchInner.Restart();
                        ScreenshotUtilities.SaveBitmapWithShapes(fastBitmap, debugBounds, debugHashes, imagePath + "screenshot-markup.png");
                        stopwatchInner.Stop();
                        if (bDebugMode) { Logger.WriteLine("Screenshot save: " + stopwatchInner.ElapsedMilliseconds + "ms"); }
                    }
                }
            }
            else
            {
                currentState = EState.MissingGameWindow;
                currentTurnState = ETurnState.MissingTimer;
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("Screenshot TOTAL: " + stopwatch.ElapsedMilliseconds + "ms"); }
        }

        public EState GetCurrentState()
        {
            return currentState;
        }

        public ETurnState GetCurrentTurnState()
        {
            return currentTurnState;
        }

        public Rectangle GetGameWindowRect()
        {
            return cachedGameWindow;
        }

        public Rectangle GetGridRect()
        {
            return cachedGridCoord;
        }

        public Rectangle GetRuleBoxRect()
        {
            return cachedRuleBox;
        }

        public Rectangle GetBlueCardRect(int Idx)
        {
            return cachedBlueCards[Idx];
        }

        public Rectangle GetBoardCardRect(int Idx)
        {
            return cachedBoardCards[Idx];
        }

        public Image GetDebugScreenshot()
        {
            return debugScreenshot;
        }

        public bool IsInScanArea(Point testPt)
        {
            return cachedScanAreaBox.Contains(testPt.X - cachedGameWindow.Left, testPt.Y - cachedGameWindow.Top);
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        private HandleRef FindGameWindow()
        {
            HandleRef WindowHandle = new HandleRef();

            if (cachedProcess == null || !cachedProcess.MainWindowTitle.StartsWith("FINAL FANTASY"))
            {
                Process[] processes = Process.GetProcessesByName("ffxiv_dx11");
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName("ffxiv");
                }

                cachedProcess = null;
                foreach (Process p in processes)
                {
                    if (p.MainWindowTitle.StartsWith("FINAL FANTASY"))
                    {
                        cachedProcess = p;
                        break;
                    }
                }
            }

            if (cachedProcess != null)
            {
                WindowHandle = new HandleRef(this, cachedProcess.MainWindowHandle);
            }
            else
            {
                currentState = EState.MissingGameProcess;
            }

            return WindowHandle;
        }

        public Rectangle FindGameWindowBounds()
        {
            Rectangle result = new Rectangle(0, 0, 0, 0);

            HandleRef windowHandle = FindGameWindow();
            bool bHasWindow = windowHandle.Handle.ToInt64() != 0;
            if (bHasWindow)
            {
                if (GetWindowRect(windowHandle, out RECT windowRectApi))
                {
                    result = new Rectangle(windowRectApi.Left, windowRectApi.Top, windowRectApi.Right - windowRectApi.Left, windowRectApi.Bottom - windowRectApi.Top);
                }
            }

            return result;
        }

        private Bitmap TakeScreenshot(HandleRef WindowHandle)
        {
            Bitmap bitmap = null;
            if (GetWindowRect(WindowHandle, out RECT windowRectApi))
            {
                cachedGameWindow = new Rectangle(windowRectApi.Left, windowRectApi.Top, windowRectApi.Right - windowRectApi.Left, windowRectApi.Bottom - windowRectApi.Top);
                bitmap = new Bitmap(cachedGameWindow.Width, cachedGameWindow.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    bool bIsNewerThanWindows7 = (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                        (Environment.OSVersion.Version.Major > 6) || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor > 1));

                    if (bIsNewerThanWindows7)
                    {
                        // can't use PrintWindow API above win7, returns black screen
                        // copy entire screen - will capture all windows on top of game too
                        g.CopyFromScreen(cachedGameWindow.Location, Point.Empty, cachedGameWindow.Size);
                    }
                    else
                    {
                        IntPtr hdcBitmap;
                        try
                        {
                            hdcBitmap = g.GetHdc();
                        }
                        catch
                        {
                            return null;
                        }

                        // capture window contents only
                        PrintWindow(WindowHandle.Handle, hdcBitmap, 0);
                        g.ReleaseHdc(hdcBitmap);
                    }
                }
            }
            else
            {
                currentState = EState.MissingGameWindow;
            }

            return bitmap;
        }

        private Bitmap TakeScreenshotPartial(HandleRef WindowHandle, Rectangle innerBounds)
        {
            Bitmap bitmap = null;
            if (innerBounds.Width > 0 && GetWindowRect(WindowHandle, out RECT windowRectApi))
            {
                bitmap = new Bitmap(innerBounds.Width, innerBounds.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // copy entire screen - will capture all windows on top of game too
                    Point copyPt = new Point(windowRectApi.Left + innerBounds.Left, windowRectApi.Top + innerBounds.Top);
                    g.CopyFromScreen(copyPt, Point.Empty, innerBounds.Size);
                }
            }
            else
            {
                currentState = EState.MissingGameWindow;
            }

            return bitmap;
        }

        private Bitmap LoadTestScreenshot(string path)
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }

        private Rectangle FindGridCoords(FastBitmapHSV bitmap)
        {
            Rectangle GridRect = new Rectangle();

            // 1. find mid-upper left vertex of grid
            // 2. adjust Y to be left top corner of grid
            // 3. check if grid is matching given tile size

            int minTileSize = bitmap.Width * 5 / 100;
            int maxTileSize = bitmap.Width * 15 / 100;
            int maxScanX = bitmap.Width - (minTileSize * 3) - 20;
            int maxScanY = bitmap.Height - (minTileSize * 2) - 20;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // since it's all centered by default, scan from center of image
            bool bScanning = true;
            for (int IdxY = 0; IdxY < maxScanY && bScanning; IdxY++)
            {
                int ScanY = (bitmap.Height / 2) + ((IdxY / 2) * ((IdxY % 2 == 0) ? 1 : -1));
                for (int IdxX = 0; IdxX < maxScanX && bScanning; IdxX++)
                {
                    if (HasGridVertexMatch(bitmap, IdxX, ScanY, ESide.Up | ESide.Down | ESide.Right))
                    {
                        for (int IdxTileSize = minTileSize; IdxTileSize < maxTileSize; IdxTileSize++)
                        {
                            int maxTileScanX = IdxX + (3 * IdxTileSize) + 10;
                            int maxTileScanY = ScanY + (2 * IdxTileSize) + 10;
                            int minTileScanY = IdxTileSize + 10;

                            if (maxTileScanX >= bitmap.Width || maxTileScanY >= bitmap.Height || ScanY < minTileScanY)
                            {
                                break;
                            }

                            if (HasGridMatch(bitmap, IdxX, ScanY - IdxTileSize, IdxTileSize))
                            {
                                GridRect = new Rectangle(IdxX, ScanY - IdxTileSize, IdxTileSize * 3, IdxTileSize * 3);
                                bScanning = false;
                                break;
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("FindGridCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }

            return GridRect;
        }

        private bool HasGridVertexMatch(FastBitmapHSV bitmap, int posX, int posY, ESide sides, bool bDebugDetection = false)
        {
            const int vertexTestSpacing = 8;

            FastPixelHSV testPx = bitmap.GetPixel(posX, posY);
            if (!colorMatchGridBorder.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: at vertex " + testPx); }
                return false;
            }

            if ((sides & ESide.Up) != ESide.None)
            {
                FastPixelHSV testPxBU = bitmap.GetPixel(posX, posY - vertexTestSpacing);
                if (!colorMatchGridBorder.IsMatching(testPxBU))
                {
                    if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: border up " + testPxBU); }
                    return false;
                }

                if ((sides & ESide.Left) != ESide.None)
                {
                    FastPixelHSV testPxFUL = bitmap.GetPixel(posX - vertexTestSpacing, posY - vertexTestSpacing);
                    if (!colorMatchGridField.IsMatching(testPxFUL))
                    {
                        if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: field up-left " + testPxFUL); }
                        return false;
                    }
                }

                if ((sides & ESide.Right) != ESide.None)
                {
                    FastPixelHSV testPxFUR = bitmap.GetPixel(posX + vertexTestSpacing, posY - vertexTestSpacing);
                    if (!colorMatchGridField.IsMatching(testPxFUR))
                    {
                        if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: field up-right " + testPxFUR); }
                        return false;
                    }
                }
            }

            if ((sides & ESide.Down) != ESide.None)
            {
                FastPixelHSV testPxBD = bitmap.GetPixel(posX, posY + vertexTestSpacing);
                if (!colorMatchGridBorder.IsMatching(testPxBD))
                {
                    if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: border down " + testPxBD); }
                    return false;
                }

                if ((sides & ESide.Left) != ESide.None)
                {
                    FastPixelHSV testPxFDL = bitmap.GetPixel(posX - vertexTestSpacing, posY + vertexTestSpacing);
                    if (!colorMatchGridField.IsMatching(testPxFDL))
                    {
                        if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: field down-left " + testPxFDL); }
                        return false;
                    }
                }

                if ((sides & ESide.Right) != ESide.None)
                {
                    FastPixelHSV testPxFDR = bitmap.GetPixel(posX + vertexTestSpacing, posY + vertexTestSpacing);
                    if (!colorMatchGridField.IsMatching(testPxFDR))
                    {
                        if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: field down-right " + testPxFDR); }
                        return false;
                    }
                }
            }

            if ((sides & ESide.Left) != ESide.None)
            {
                FastPixelHSV testPxBL = bitmap.GetPixel(posX - vertexTestSpacing, posY);
                if (!colorMatchGridBorder.IsMatching(testPxBL))
                {
                    if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: border left " + testPxBL); }
                    return false;
                }
            }

            if ((sides & ESide.Right) != ESide.None)
            {
                FastPixelHSV testPxBR = bitmap.GetPixel(posX + vertexTestSpacing, posY);
                if (!colorMatchGridBorder.IsMatching(testPxBR))
                {
                    if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] failed: border right " + testPxBR); }
                    return false;
                }
            }

            if (bDebugDetection) { Logger.WriteLine("vertex [" + posX + ", " + posY + "] passed (anchors: " + sides + ")"); }
            return true;
        }

        private bool HasGridMatch(FastBitmapHSV bitmap, int posX, int posY, int cellSize, bool bDebugDetection = false)
        {
            bool bMatch = false;

            if (bDebugDetection) { Logger.WriteLine("HasGridMatch at [" + posX + ", " + posY + "], tile: " + cellSize + "..."); }

            // find:
            // - edges, except corners (rounded)
            // - at least 1 from middle

            int NumEdgeVerts = 1 +
                // left edge, ignore upper
                (HasGridVertexMatch(bitmap, posX, posY + (cellSize * 2), ESide.Up | ESide.Down | ESide.Right, bDebugDetection) ? 1 : 0) +
                // top edge
                (HasGridVertexMatch(bitmap, posX + cellSize, posY, ESide.Left | ESide.Down | ESide.Right, bDebugDetection) ? 1 : 0) +
                (HasGridVertexMatch(bitmap, posX + (cellSize * 2), posY, ESide.Left | ESide.Down | ESide.Right, bDebugDetection) ? 1 : 0) +
                // right edge
                (HasGridVertexMatch(bitmap, posX + (cellSize * 3), posY + cellSize, ESide.Up | ESide.Down | ESide.Left, bDebugDetection) ? 1 : 0) +
                (HasGridVertexMatch(bitmap, posX + (cellSize * 3), posY + (cellSize * 2), ESide.Up | ESide.Down | ESide.Left, bDebugDetection) ? 1 : 0) +
                // bottom edge
                (HasGridVertexMatch(bitmap, posX + cellSize, posY + (cellSize * 3), ESide.Left | ESide.Up | ESide.Right, bDebugDetection) ? 1 : 0) +
                (HasGridVertexMatch(bitmap, posX + (cellSize * 2), posY + (cellSize * 3), ESide.Left | ESide.Up | ESide.Right, bDebugDetection) ? 1 : 0);

            if (NumEdgeVerts >= 6)
            {
                bool bHasAnyMidVert =
                    HasGridVertexMatch(bitmap, posX + cellSize, posY + cellSize, ESide.Left | ESide.Right | ESide.Up | ESide.Down, bDebugDetection) ||
                    HasGridVertexMatch(bitmap, posX + (cellSize * 2), posY + cellSize, ESide.Left | ESide.Right | ESide.Up | ESide.Down, bDebugDetection) ||
                    HasGridVertexMatch(bitmap, posX + cellSize, posY + (cellSize * 2), ESide.Left | ESide.Right | ESide.Up | ESide.Down, bDebugDetection) ||
                    HasGridVertexMatch(bitmap, posX + (cellSize * 2), posY + (cellSize * 2), ESide.Left | ESide.Right | ESide.Up | ESide.Down, bDebugDetection);

                if (bHasAnyMidVert)
                {
                    bMatch = true;
                }
            }

            return bMatch;
        }

        private Rectangle FindRuleBoxCoords(FastBitmapHSV bitmap, Rectangle gridRect)
        {
            Rectangle RuleBoxRect = new Rectangle();

            // 1. scan line from grid's top right to the right
            // 2. find saturation edges
            // 3. run vertical line and do the same

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int traceStart = gridRect.Right + 10;
            int traceEnd = Math.Min(bitmap.Width, gridRect.Right + gridRect.Width);
            int traceY = gridRect.Top + (gridRect.Height / 9);

            int AttemptIdx = 0;
            bool bShouldRetry = true;
            bool bUsingLowScan = false;
            while (bShouldRetry)
            {
                bShouldRetry = false;
                AttemptIdx++;

                List<int> segPosH = ScreenshotUtilities.TraceLineSegments(bitmap, traceStart, traceY, 1, 0, traceEnd - traceStart, colorMatchRuleBox, 20, 2);
                if (segPosH.Count == 2)
                {
                    int lenSegH = segPosH[1] - segPosH[0];
                    if (lenSegH < (gridRect.Width / 3))
                    {
                        traceY = gridRect.Top + (gridRect.Height * 15 / 100);
                        traceStart += 50;
                        bShouldRetry = true;
                        bUsingLowScan = true;
                        continue;
                    }

                    int traceEndY0 = gridRect.Top - (gridRect.Height / 15);

                    List<int> segPosVT = ScreenshotUtilities.TraceLineSegments(bitmap, segPosH[0] + 50, traceY, 0, -1, traceY - traceEndY0, colorMatchRuleBox, 5, 2);
                    if (segPosVT.Count == 2)
                    {
                        int traceEndY1 = gridRect.Top + (gridRect.Height / 6);

                        List<int> segPosVB = ScreenshotUtilities.TraceLineSegments(bitmap, segPosH[0] + 50, traceY, 0, 1, traceEndY1 - traceY, colorMatchRuleBox, 5, 2);
                        if (segPosVB.Count == 2 || bUsingLowScan)
                        {
                            int ruleBoxWidth = segPosH[1] - segPosH[0];
                            int ruleBoxHeight = (segPosVB.Count == 2 ? segPosVB[1] : traceY) - segPosVT[1];
                            float ruleBoxRatio = (float)ruleBoxWidth / ruleBoxHeight;
                            float expectedRatio = 3.15f;
                            float diff = Math.Abs(ruleBoxRatio - expectedRatio);
                            if (diff < 0.25f)
                            {
                                RuleBoxRect = new Rectangle(segPosH[0], segPosVT[1], ruleBoxWidth, ruleBoxHeight);
                            }
                            else if (AttemptIdx < 10)
                            {
                                bShouldRetry = true;
                                traceY -= 2;
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("FindRuleBoxCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }

            return RuleBoxRect;
        }

        private Rectangle FindTimerBox(FastBitmapHSV bitmap, Rectangle gridRect, Rectangle ruleRect)
        {
            Rectangle result = new Rectangle();
            int gridMidX = (gridRect.Left + gridRect.Right) / 2;
            int ruleMidX = (ruleRect.Left + ruleRect.Right) / 2;
            int traceStartX = gridMidX - (ruleMidX - gridMidX);
            int traceStartY = ruleRect.Bottom;

            bool bHasTop = ScreenshotUtilities.TraceLine(bitmap, traceStartX, traceStartY, 0, 1, gridRect.Height / 3, colorMatchTimerBox, out Point hitTop);
            if (bHasTop)
            {
                bool bHasBottom = ScreenshotUtilities.TraceLine(bitmap, traceStartX, hitTop.Y + 10, 0, -1, 10, colorMatchTimerBox, out Point hitBottom);
                if (bHasBottom)
                {
                    int boxRight = gridMidX - (ruleRect.Left - gridMidX);
                    int boxWidth = ruleRect.Width * 80 / 100;
                    result = new Rectangle(boxRight - boxWidth, hitTop.Y + 1, boxWidth, hitBottom.Y - hitTop.Y - 2);
                }
            }

            return result;
        }

        private Rectangle FindTimerBox(FastBitmapHSV bitmap, Rectangle timerScanRect)
        {
            Rectangle result = new Rectangle();
            int traceStartX = timerScanRect.Width / 2;
            bool bHasTop = ScreenshotUtilities.TraceLine(bitmap, traceStartX, 0, 0, 1, timerScanRect.Height * 2 / 3, colorMatchTimerBox, out Point hitTop);
            if (bHasTop)
            {
                bool bHasBottom = ScreenshotUtilities.TraceLine(bitmap, traceStartX, hitTop.Y + 10, 0, -1, 10, colorMatchTimerBox, out Point hitBottom);
                if (bHasBottom)
                {
                    result = new Rectangle(0, hitTop.Y, timerScanRect.Width, hitBottom.Y - hitTop.Y);
                }
            }

            return result;
        }

        private Rectangle[] FindBlueCardCoords(FastBitmapHSV bitmap, Rectangle gridRect)
        {
            Rectangle[] result = null;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Size approxCardSize = new Size((gridRect.Width / 3) * 80 / 100, (gridRect.Height / 3));
            int midLineTop = (gridRect.Top + gridRect.Bottom) / 2;
            int midLineBottom = gridRect.Top + (gridRect.Height * 84 / 100);
            int offsetTop = approxCardSize.Width * 70 / 100;
            int offsetBottom = approxCardSize.Width * 120 / 100;
            int traceLen = approxCardSize.Width / 6;

            Point[] scanAnchors = new Point[5]
            {
                new Point(gridRect.Left - offsetTop - (approxCardSize.Width * 2), midLineTop),
                new Point(gridRect.Left - offsetTop - approxCardSize.Width, midLineTop),
                new Point(gridRect.Left - offsetTop, midLineTop),
                new Point(gridRect.Left - offsetBottom - approxCardSize.Width, midLineBottom),
                new Point(gridRect.Left - offsetBottom, midLineBottom),
            };

            Rectangle[] scanRect = new Rectangle[5];

            int numFoundCards = 0;
            for (int Idx = 0; Idx < scanAnchors.Length; Idx++)
            {
                bool bHasHit0 = ScreenshotUtilities.TraceLine(bitmap, scanAnchors[Idx].X, scanAnchors[Idx].Y - (approxCardSize.Height / 2), 0, 1, traceLen, colorMatchCardBorder, out Point hitTop);
                bool bHasHit1 = ScreenshotUtilities.TraceLine(bitmap, scanAnchors[Idx].X, scanAnchors[Idx].Y + (approxCardSize.Height / 2), 0, -1, traceLen, colorMatchCardBorder, out Point hitBottom);
                bool bHasHit2 = ScreenshotUtilities.TraceLine(bitmap, scanAnchors[Idx].X - (approxCardSize.Width / 2), scanAnchors[Idx].Y, 1, 0, traceLen, colorMatchCardBorder, out Point hitLeft);
                bool bHasHit3 = ScreenshotUtilities.TraceLine(bitmap, scanAnchors[Idx].X + (approxCardSize.Width / 2), scanAnchors[Idx].Y, -1, 0, traceLen, colorMatchCardBorder, out Point hitRight);

                if (bHasHit0 && bHasHit1 && bHasHit2 && bHasHit3)
                {
                    scanRect[Idx] = new Rectangle(hitLeft.X - scanAnchors[Idx].X, hitTop.Y - scanAnchors[Idx].Y, hitRight.X - hitLeft.X, hitBottom.Y - hitTop.Y);
                    //if (bDebugMode) { debugShapes.Add(new Rectangle(hitLeft.X, hitTop.Y, scanRect[Idx].Width, scanRect[Idx].Height)); }
                    numFoundCards++;
                }
            }

            if (numFoundCards > 0)
            {
                if (numFoundCards < 5)
                {
                    // find medians for each param of scanRect and use them for missing
                    List<int> sortListX = new List<int>();
                    List<int> sortListY = new List<int>();
                    List<int> sortListW = new List<int>();
                    List<int> sortListH = new List<int>();

                    for (int Idx = 0; Idx < scanRect.Length; Idx++)
                    {
                        if (scanRect[Idx].Width > 0)
                        {
                            sortListX.Add(scanRect[Idx].X);
                            sortListY.Add(scanRect[Idx].Y);
                            sortListW.Add(scanRect[Idx].Width);
                            sortListH.Add(scanRect[Idx].Height);
                        }
                    }

                    sortListX.Sort();
                    sortListY.Sort();
                    sortListW.Sort();
                    sortListH.Sort();

                    int medianX = ((sortListX.Count % 2) == 0) ? ((sortListX[(sortListX.Count / 2) - 1] + sortListX[sortListX.Count / 2]) / 2) : sortListX[sortListX.Count / 2];
                    int medianY = ((sortListY.Count % 2) == 0) ? ((sortListY[(sortListY.Count / 2) - 1] + sortListY[sortListY.Count / 2]) / 2) : sortListY[sortListY.Count / 2];
                    int medianW = ((sortListW.Count % 2) == 0) ? ((sortListW[(sortListW.Count / 2) - 1] + sortListW[sortListW.Count / 2]) / 2) : sortListW[sortListW.Count / 2];
                    int medianH = ((sortListH.Count % 2) == 0) ? ((sortListH[(sortListH.Count / 2) - 1] + sortListH[sortListH.Count / 2]) / 2) : sortListH[sortListH.Count / 2];

                    for (int Idx = 0; Idx < scanRect.Length; Idx++)
                    {
                        if (scanRect[Idx].Width == 0)
                        {
                            scanRect[Idx] = new Rectangle(medianX, medianY, medianW, medianH);
                        }
                    }
                }

                result = new Rectangle[scanRect.Length];
                for (int Idx = 0; Idx < scanRect.Length; Idx++)
                {
                    result[Idx] = new Rectangle(scanRect[Idx].X + scanAnchors[Idx].X, scanRect[Idx].Y + scanAnchors[Idx].Y, scanRect[Idx].Width, scanRect[Idx].Height);
                }
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("FindBuleCardCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }

            return result;
        }

        private Rectangle[] FindRedCardCoords(Rectangle gridRect, Rectangle[] blueCards)
        {
            int gridAxisV = (gridRect.Right + gridRect.Left) / 2;

            Rectangle[] result = new Rectangle[5]
            {
                new Rectangle(gridAxisV + (gridAxisV - blueCards[2].Right), blueCards[2].Top, blueCards[0].Width, blueCards[0].Height),
                new Rectangle(gridAxisV + (gridAxisV - blueCards[1].Right), blueCards[1].Top, blueCards[0].Width, blueCards[0].Height),
                new Rectangle(gridAxisV + (gridAxisV - blueCards[0].Right), blueCards[0].Top, blueCards[0].Width, blueCards[0].Height),
                new Rectangle(gridAxisV + (gridAxisV - blueCards[4].Right), blueCards[4].Top, blueCards[0].Width, blueCards[0].Height),
                new Rectangle(gridAxisV + (gridAxisV - blueCards[3].Right), blueCards[3].Top, blueCards[0].Width, blueCards[0].Height)
            };

            return result;
        }

        private Rectangle[] FindBoardCardCoords(Rectangle gridRect, Rectangle[] blueCards)
        {
            Rectangle[] result = new Rectangle[9];

            int cardWidth = blueCards[0].Width;
            int cardHeight = blueCards[0].Height;

            int cellCenterX0 = gridRect.Left + (gridRect.Width / 6);
            int cellCenterY0 = gridRect.Top + (gridRect.Height / 6);

            for (int IdxY = 0; IdxY < 3; IdxY++)
            {
                for (int IdxX = 0; IdxX < 3; IdxX++)
                {
                    int cellCenterX = cellCenterX0 + (gridRect.Width / 3) * IdxX;
                    int cellCenterY = cellCenterY0 + (gridRect.Height / 3) * IdxY;

                    result[IdxX + (IdxY * 3)] = new Rectangle(cellCenterX - (cardWidth / 2), cellCenterY - (cardHeight / 2), cardWidth, cardHeight);
                }
            }

            return result;
        }

        private void ParseRules(FastBitmapHSV bitmap, Rectangle rulesRect, List<TriadGameModifier> rules)
        {
            rules.Clear();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Rectangle noTokenRect = new Rectangle(rulesRect.Left, rulesRect.Top, rulesRect.Width * 80 / 100, rulesRect.Height);
            List<Point> spanV = ScreenshotUtilities.TraceSpansV(bitmap, noTokenRect, colorMatchRuleText, 5);
            foreach (Point span in spanV)
            {
                Point boundsH = ScreenshotUtilities.TraceBoundsH(bitmap, new Rectangle(rulesRect.Left, span.X, rulesRect.Width, span.Y), colorMatchRuleText, span.Y * 2);
                if (boundsH.Y > 5)
                {
                    Rectangle ruleTextBox = new Rectangle(boundsH.X, span.X, boundsH.Y, span.Y);
                    FastBitmapHash ruleHashData = ScreenshotUtilities.CalculateImageHash(bitmap, ruleTextBox, rulesRect, colorMatchRuleText, 64, 8);

                    TriadGameModifier bestModOb = (TriadGameModifier)ScreenshotUtilities.FindMatchingHash(ruleHashData, EImageHashType.Rule, out int bestDistance, bDebugMode);
                    if (bestModOb == null)
                    {
                        ScreenshotUtilities.ConditionalAddUnknownHash(
                            ScreenshotUtilities.CreateUnknownImageHash(ruleHashData, cachedScreenshot, EImageHashType.Rule), unknownHashes);

                        currentState = EState.UnknownHash;
                    }
                    else
                    {
                        ruleHashData.GuideOb = bestModOb;
                        currentHashDetections.Add(ruleHashData, bestDistance);

                        rules.Add(bestModOb);
                    }

                    debugHashes.Add(ruleHashData);
                }
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("ParseRules: " + stopwatch.ElapsedMilliseconds + "ms"); }
        }

        private TriadCard ParseCard(FastBitmapHSV bitmap, Rectangle cardRect, List<ImagePatternDigit> digitList, string debugName, out bool bIsGreyedOut)
        {
            bIsGreyedOut = false;

            // check if card is there
            int MidX = (cardRect.Left + cardRect.Right) / 2;
            int borderMatchCount = 0;
            for (int PosY = 0; PosY < 10; PosY++)
            {
                FastPixelHSV testPx = bitmap.GetPixel(MidX, PosY + cardRect.Top);
                borderMatchCount += colorMatchCardBorder.IsMatching(testPx) ? 1 : 0;
            }

            bool bHasCard = borderMatchCount >= 4;
            if (!bHasCard)
            {
                if (bDebugMode) { Logger.WriteLine("ParseCard(" + debugName + "): empty, counter:" + borderMatchCount); }
                return null;
            }

            // check if card is hidden based on approx numberbox location
            Rectangle numberBox = new Rectangle(cardRect.Left + (cardRect.Width * 27 / 100),    // moved slightly off center to right
                cardRect.Top + (cardRect.Height * 74 / 100),
                cardRect.Width * 50 / 100,
                cardRect.Height * 18 / 100);

            float borderPct = ScreenshotUtilities.CountFillPct(bitmap, numberBox, colorMatchCardBorder);
            if (borderPct > 0.75f)
            {
                if (bDebugMode) { Logger.WriteLine("ParseCard(" + debugName + "): hidden, fill:" + (int)(100 * borderPct) + "%"); }
                return TriadCardDB.Get().hiddenCard;
            }

            ScreenshotUtilities.FindColorRange(bitmap, numberBox, out int minMono, out int maxMono);
            FastPixelMatch colorMatchNumAdjusted = (maxMono < 220) ? new FastPixelMatchMono((byte)(maxMono * 85 / 100), 255) : colorMatchCardNumber;
            bIsGreyedOut = (maxMono < 220);

            // find numbers
            if (bDebugMode) { debugShapes.Add(new Rectangle(numberBox.Left - 1, numberBox.Top - 1, numberBox.Width + 2, numberBox.Height + 2)); }

            int numberBoxMidX = (numberBox.Left + numberBox.Right) / 2;
            int numberBoxMidY = (numberBox.Top + numberBox.Bottom) / 2;
            int approxCardHeight = numberBox.Height * 45 / 100;
            int approxCardWidth = numberBox.Width / 3;

            Point[] cardScanAnchors = new Point[4]
            {
                new Point(numberBoxMidX - (approxCardWidth / 2), numberBox.Top),
                new Point(numberBoxMidX - (approxCardWidth / 2), numberBox.Bottom - approxCardHeight),
                new Point(numberBoxMidX + (approxCardWidth * 40 / 100), numberBoxMidY - (approxCardHeight / 2)),
                new Point(numberBoxMidX - (approxCardWidth * 110 / 100), numberBoxMidY - (approxCardHeight / 2)),
            };

            CardState detectionState = new CardState();
            detectionState.sideImage = new ImageDataDigit[4];

            int[] CardNumbers = new int[4]
            {
                ScreenshotUtilities.FindPatternMatch(bitmap, cardScanAnchors[0], colorMatchNumAdjusted, digitList, out detectionState.sideImage[0]),
                ScreenshotUtilities.FindPatternMatch(bitmap, cardScanAnchors[1], colorMatchNumAdjusted, digitList, out detectionState.sideImage[1]),
                ScreenshotUtilities.FindPatternMatch(bitmap, cardScanAnchors[2], colorMatchNumAdjusted, digitList, out detectionState.sideImage[2]),
                ScreenshotUtilities.FindPatternMatch(bitmap, cardScanAnchors[3], colorMatchNumAdjusted, digitList, out detectionState.sideImage[3]),
            };

            TriadCard foundCard = TriadCardDB.Get().Find(CardNumbers[0], CardNumbers[1], CardNumbers[2], CardNumbers[3]);
            if (bDebugMode)
            {
                string descFoundCards = "";
                if (foundCard != null)
                {
                    if (foundCard.SameNumberId < 0)
                    {
                        descFoundCards = foundCard.Name;
                    }
                    else
                    {
                        foreach (TriadCard card in TriadCardDB.Get().sameNumberMap[foundCard.SameNumberId])
                        {
                            descFoundCards += card.Name + ", ";
                        }

                        descFoundCards = descFoundCards.Remove(descFoundCards.Length - 2, 2) + " (needs hash check)";
                    }
                }
                else
                {
                    descFoundCards = "none";
                }

                Logger.WriteLine("ParseCard(" + debugName + "): " + CardNumbers[0] + ", " + CardNumbers[2] + ", " + CardNumbers[1] + ", " + CardNumbers[3] + " => " + descFoundCards);
            }

            // more than one card found
            if (foundCard != null && foundCard.SameNumberId >= 0)
            {
                Rectangle cardHashBox = new Rectangle(cardRect.Left + (cardRect.Width * 15 / 100), cardRect.Top + (cardRect.Height * 70 / 100),
                    cardRect.Width * 70 / 100, cardRect.Height * 25 / 100);

                FastPixelMatch colorMatchAll = new FastPixelMatchMono(0, 255);
                FastBitmapHash cardHashData = ScreenshotUtilities.CalculateImageHash(bitmap, cardHashBox, cardRect, colorMatchAll, 32, 8);
                cardHashData.GuideOb = foundCard;

                TriadCard bestCardOb = (TriadCard)ScreenshotUtilities.FindMatchingHash(cardHashData, EImageHashType.Card, out int bestDistance, bDebugMode);
                if (bestCardOb == null)
                {
                    ScreenshotUtilities.ConditionalAddUnknownHash(
                        ScreenshotUtilities.CreateUnknownImageHash(cardHashData, cachedScreenshot, EImageHashType.Card), unknownHashes);

                    currentState = EState.UnknownHash;
                }
                else
                {
                    foundCard = bestCardOb;

                    cardHashData.GuideOb = bestCardOb;
                    currentHashDetections.Add(cardHashData, bestDistance);
                }

                debugHashes.Add(cardHashData);
            }

            detectionState.name = debugName;
            detectionState.card = foundCard;
            detectionState.failedMatching = (foundCard == null);
            detectionState.sideNumber = new int[4] { CardNumbers[0], CardNumbers[1], CardNumbers[2], CardNumbers[3] };
            currentCardState.Add(detectionState);

            if (foundCard == null)
            {
                foundCard = failedMatchCard;
            }

            return foundCard;
        }

        private ETriadCardOwner ParseCardOwner(FastBitmapHSV bitmap, Rectangle cardRect, string debugName)
        {
            ETriadCardOwner owner = ETriadCardOwner.Unknown;

            int testWidth = cardRect.Width / 4;
            Rectangle[] testBounds = new Rectangle[2]
            {
                new Rectangle(cardRect.Left, cardRect.Top + (cardRect.Height / 4), testWidth, cardRect.Height / 2),
                new Rectangle(cardRect.Right - testWidth, cardRect.Top + (cardRect.Height / 4), testWidth, cardRect.Height / 2),
            };       

            int counterRed = 0;
            int counterBlue = 0;
            foreach (Rectangle rect in testBounds)
            {
                for (int IdxY = rect.Top; IdxY < rect.Bottom; IdxY++)
                {
                    for (int IdxX = rect.Left; IdxX < rect.Right; IdxX++)
                    {
                        FastPixelHSV testPx = bitmap.GetPixel(IdxX, IdxY);
                        counterRed += (colorMatchCardOwnerRed1.IsMatching(testPx) || colorMatchCardOwnerRed2.IsMatching(testPx)) ? 1 : 0;
                        counterBlue += colorMatchCardOwnerBlue.IsMatching(testPx) ? 1 : 0;
                    }
                }

                if (bDebugMode) { debugShapes.Add(rect); }
            }

            owner = (counterRed > 10 || counterBlue > 10) ? (counterRed > counterBlue ? ETriadCardOwner.Red : ETriadCardOwner.Blue) : ETriadCardOwner.Unknown;
            if (bDebugMode) { Logger.WriteLine(">> owner: " + owner + " (" + counterRed + " red vs " + counterBlue + " blue)"); }
            return owner;
        }

        private bool ParseTimer(FastBitmapHSV bitmap, Rectangle timerRect)
        {
            int scanY = (timerRect.Top + timerRect.Bottom) / 2;
            bool bHasActiveTimer = ScreenshotUtilities.TraceLine(bitmap, timerRect.Left, scanY, 1, 0, timerRect.Width, colorMatchTimerActive, out Point dummyHit);
            if (bDebugMode) { Logger.WriteLine("ParseTimer: " + (bHasActiveTimer ? "blue turn" : "waiting...")); }
            return bHasActiveTimer;
        }
    }
}