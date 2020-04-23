using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
            Cactpot = 128,
            All = Decks | Rules | Board | Cactpot,
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

        public enum EGame
        {
            TripleTriad,
            MiniCactpot,
        }

        public class GameStateTriad
        {
            public TriadCard[] board;
            public ETriadCardOwner[] boardOwner;
            public TriadCard[] blueDeck;
            public TriadCard[] redDeck;
            public TriadCard forcedBlueCard;
            public List<TriadGameModifier> mods;

            public GameStateTriad()
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

        public class GameStateCactpot
        {
            public int[] board;
            public int numRevealed;

            public GameStateCactpot()
            {
                board = new int[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                numRevealed = 0;
            }           
        }

        private FastPixelMatch colorMatchGridBorder = new FastPixelMatchMono(0, 150);
        private FastPixelMatch colorMatchGridField = new FastPixelMatchMono(170, 255);
        private FastPixelMatch colorMatchRuleBox = new FastPixelMatchMono(20, 80);
        private FastPixelMatch colorMatchDeckBack = new FastPixelMatchHSV(275, 325, 0, 100, 0, 100);
        private FastPixelMatch colorMatchRuleText = new FastPixelMatchMono(150, 255);
        private FastPixelMatch colorMatchCardBorder = new FastPixelMatchHSV(20, 40, 0, 100, 0, 100);
        private FastPixelMatch colorMatchCardNumber = new FastPixelMatchMono(220, 255);
        private FastPixelMatch colorMatchCardOwnerRed1 = new FastPixelMatchHueMono(0, 30, 150, 255);
        private FastPixelMatch colorMatchCardOwnerRed2 = new FastPixelMatchHueMono(330, 360, 150, 255);
        private FastPixelMatch colorMatchCardOwnerBlue = new FastPixelMatchHueMono(200, 280, 150, 255);
        private FastPixelMatch colorMatchTimerBox = new FastPixelMatchHSV(40, 60, 10, 40, 0, 100);
        private FastPixelMatch colorMatchTimerActive = new FastPixelMatchMono(80, 255);
        private FastPixelMatch colorMatchCactpotBack = new FastPixelMatchMono(0, 80);
        private FastPixelMatch colorMatchCactpotCircleFade = new FastPixelMatchHueMono(30, 60, 40, 120);
        private FastPixelMatch colorMatchCactpotCircleOut = new FastPixelMatchHueMono(30, 60, 80, 255);
        private FastPixelMatch colorMatchCactpotCircleIn = new FastPixelMatchMono(110, 255);
        private FastPixelMatch colorMatchCactpotNumber = new FastPixelMatchMono(0, 110);

        private Process cachedProcess;
        private Screen cachedScreen;
        private Rectangle cachedGameWindow;
        private Bitmap cachedScreenshot;
        private Bitmap debugScreenshot;
        private Size cachedImageSize;
        private float cachedScreenScaling;
        private Rectangle cachedGridBox;
        private Rectangle cachedScanAreaBox;
        private Rectangle cachedRuleBox;
        private Rectangle cachedTimerBox;
        private Rectangle cachedTimerScanBox;
        private Rectangle[] cachedBlueCards;
        private Rectangle[] cachedRedCards;
        private Rectangle[] cachedBoardCards;
        private Rectangle cachedCactpotBox;
        private Rectangle[] cachedCactpotCircles;
        private bool[] bHasCachedData = new bool[2];
        private bool bDebugMode = false;
        private List<ImagePatternDigit> digitMasks = new List<ImagePatternDigit>();
        private List<FastBitmapHash> debugHashes = new List<FastBitmapHash>();
        private List<Rectangle> debugShapes = new List<Rectangle>();
        private TriadCard failedMatchCard = new TriadCard(-1, "failedMatch", "", ETriadCardRarity.Common, ETriadCardType.None, 0, 0, 0, 0, 0);

        private EState currentState = EState.NoErrors;
        private ETurnState currentTurnState = ETurnState.MissingTimer;
        private EGame currentGameType = EGame.TripleTriad;
        public GameStateTriad currentTriadGame = new GameStateTriad();
        public GameStateCactpot currentCactpotGame = new GameStateCactpot();
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

            currentGameType = EGame.TripleTriad;

            Stopwatch timerTotal = new Stopwatch();
            Stopwatch timerStep = new Stopwatch();
            timerTotal.Start();

            string imagePath = DoWorkLoadScreenshot(mode, timerStep);
            if (cachedScreenshot == null)
            {
                currentState = (currentState == EState.MissingGameProcess) ? currentState : EState.MissingGameWindow;
                currentTurnState = ETurnState.MissingTimer;
                return;
            }

            debugShapes.Clear();
            timerStep.Restart();
            FastBitmapHSV fastBitmap = ScreenshotUtilities.ConvertToFastBitmap(cachedScreenshot);
            timerStep.Stop();
            if (bDebugMode) { Logger.WriteLine("Screenshot convert: " + timerStep.ElapsedMilliseconds + "ms"); }
            if (Logger.IsSuperVerbose()) { Logger.WriteLine("Screenshot: " + cachedScreenshot.Width + "x" + cachedScreenshot.Height); }

            if ((mode & EMode.TurnTimerOnly) != EMode.None)
            {
                // timer only (auto scan for triple triad)
                currentGameType = EGame.TripleTriad;
                debugHashes.Clear();

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
            }
            else
            {
                // find bounds of data to analyze, use cached whenever possible
                DoWorkFindAndCacheBouds(mode, timerStep, fastBitmap);

                debugHashes.Clear();
                unknownHashes.Clear();
                currentHashDetections.Clear();

                try
                {
                    // scan image and convert to game data
                    if (bHasCachedData[(int)EGame.TripleTriad])
                    {
                        currentGameType = EGame.TripleTriad;
                        DoWorkScanTripleTriad(mode, timerStep, fastBitmap, imagePath);
                    }
                    else if (bHasCachedData[(int)EGame.MiniCactpot])
                    {
                        currentGameType = EGame.MiniCactpot;
                        DoWorkScanMiniCactpot(mode, timerStep, fastBitmap);
                    }
                    else
                    {
                        // failed to find any game, report triple triad failure
                        currentGameType = EGame.TripleTriad;
                        currentState = EState.MissingGrid;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Failed to scan screenshot: " + ex);

                    currentGameType = EGame.TripleTriad;
                    currentState = EState.MissingGrid;
                }
            }

            // debug markup
            if (bDebugMode && (currentState != EState.MissingGrid))
            {
                List<Rectangle> debugBounds = new List<Rectangle>();
                debugBounds.AddRange(debugShapes);

                if (cachedScanAreaBox.Width > 0) { debugBounds.Add(cachedScanAreaBox); }
                switch (currentGameType)
                {
                    case EGame.TripleTriad:
                        if (cachedGridBox.Width > 0) { debugBounds.Add(cachedGridBox); }
                        if (cachedRuleBox.Width > 0) { debugBounds.Add(cachedRuleBox); }
                        if (cachedTimerBox.Width > 0) { debugBounds.Add(cachedTimerBox); }
                        if (cachedBlueCards != null) { debugBounds.AddRange(cachedBlueCards); }
                        if (cachedRedCards != null) { debugBounds.AddRange(cachedRedCards); }
                        if (cachedBoardCards != null) { debugBounds.AddRange(cachedBoardCards); }
                        break;

                    case EGame.MiniCactpot:
                        if (cachedCactpotCircles != null) { debugBounds.AddRange(cachedCactpotCircles); }
                        break;

                    default: break;
                }

                timerStep.Restart();
                ScreenshotUtilities.SaveBitmapWithShapes(fastBitmap, debugBounds, debugHashes, imagePath + "screenshot-markup.png");
                timerStep.Stop();
                if (bDebugMode) { Logger.WriteLine("Screenshot save: " + timerStep.ElapsedMilliseconds + "ms"); }
            }

            timerTotal.Stop();
            if (bDebugMode) { Logger.WriteLine("Screenshot TOTAL: " + timerTotal.ElapsedMilliseconds + "ms"); }
        }

        private string DoWorkLoadScreenshot(EMode mode, Stopwatch perfTimer)
        {
            bool bTurnTimerOnly = (mode & EMode.TurnTimerOnly) != EMode.None;           
            string imagePath = AssetManager.Get().CreateFilePath("test/");
            if (!Directory.Exists(imagePath))
            {
                imagePath = AssetManager.Get().CreateFilePath(null);
                if (!Directory.Exists(imagePath))
                {
                    imagePath = Path.GetTempPath();
                    if (!Directory.Exists(imagePath))
                    {
                        if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                        cachedScreenshot = null;
                        return null;
                    }
                }
            }

            if (bTurnTimerOnly)
            {
                imagePath += "timer-";
            }

            bool bUseTestScreenshot = false;
            bDebugMode = (mode & EMode.Debug) != EMode.None;

            perfTimer.Start();

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
                    cachedScreenshot = LoadTestScreenshot(imagePath + "screenshot-scaling.jpg");
                    //cachedScreenshot = LoadTestScreenshot(imagePath + "screenshot-owner2.jpg");

                    cachedGameWindow = (cachedScreenshot != null) ? new Rectangle(0, 0, cachedScreenshot.Width, cachedScreenshot.Height) : new Rectangle();
                }
            }
            else
            {
                if (bHasWindow)
                {
                    if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                    cachedScreenshot = bTurnTimerOnly ? TakeScreenshotPartial(windowHandle, cachedTimerScanBox) : TakeScreenshot(windowHandle);

                    if (cachedScreenshot != null && bDebugMode)
                    {
                        cachedScreenshot.Save(imagePath + "screenshot-source.jpg");
                    }
                }
            }

            perfTimer.Stop();
            if (bDebugMode) { Logger.WriteLine("Screenshot load: " + perfTimer.ElapsedMilliseconds + "ms"); }

            return imagePath;
        }

        private void DoWorkFindAndCacheBouds(EMode mode, Stopwatch perfTimer, FastBitmapHSV fastBitmap)
        {
            if (cachedImageSize == null ||
                cachedImageSize.Width != fastBitmap.Width ||
                cachedImageSize.Height != fastBitmap.Height)
            {
                cachedImageSize = new Size(fastBitmap.Width, fastBitmap.Height);
                for (int Idx = 0; Idx < bHasCachedData.Length; Idx++)
                {
                    bHasCachedData[Idx] = false;
                }
            }

            if (!bHasCachedData[(int)EGame.TripleTriad] ||
                cachedGridBox.Width <= 0 ||
                !HasGridMatch(fastBitmap, cachedGridBox.Left, cachedGridBox.Top, cachedGridBox.Width / 3))
            {
                cachedBlueCards = null;
                cachedRedCards = null;
                cachedBoardCards = null;
                bHasCachedData[(int)EGame.TripleTriad] = false;

                cachedGridBox = FindGridCoords(fastBitmap);
                if (cachedGridBox.Width > 0)
                {
                    currentGameType = EGame.TripleTriad;

                    cachedRuleBox = FindRuleBoxCoords(fastBitmap, cachedGridBox);
                    cachedTimerBox = FindTimerBox(fastBitmap, cachedGridBox, cachedRuleBox);
                    cachedTimerScanBox = new Rectangle(cachedTimerBox.X, cachedTimerBox.Y - 20, cachedTimerBox.Width, cachedTimerBox.Height + 40);
                    cachedScanAreaBox = new Rectangle(cachedGridBox.Left - (cachedGridBox.Width * 85 / 100),
                        cachedGridBox.Top - (cachedGridBox.Height * 5 / 100),
                        cachedGridBox.Width * 270 / 100,
                        cachedGridBox.Height * 110 / 100);

                    cachedBlueCards = FindBlueCardCoords(fastBitmap, cachedGridBox);
                    if (cachedBlueCards != null && cachedBlueCards.Length == 5)
                    {
                        cachedRedCards = FindRedCardCoords(cachedGridBox, cachedBlueCards);
                        cachedBoardCards = FindBoardCardCoords(cachedGridBox, cachedBlueCards);
                        bHasCachedData[(int)EGame.TripleTriad] = true;
                    }
                    else
                    {
                        currentState = EState.MissingCards;
                    }
                }
                else
                {
                    if (!bHasCachedData[(int)EGame.MiniCactpot] ||
                        cachedCactpotBox.Width <= 0 ||
                        !HasCactpotMatch(fastBitmap, cachedCactpotBox.Left, cachedCactpotBox.Top, cachedCactpotBox.Width / 3))
                    {
                        bHasCachedData[(int)EGame.MiniCactpot] = false;

                        cachedCactpotBox = FindCactpotCoords(fastBitmap);
                        if (cachedCactpotBox.Width > 0)
                        {
                            currentGameType = EGame.MiniCactpot;

                            cachedScanAreaBox = new Rectangle(cachedCactpotBox.Left, cachedCactpotBox.Top, cachedCactpotBox.Width, cachedCactpotBox.Height);
                            cachedCactpotCircles = FindCactpotCircleCoords(cachedCactpotBox);
                            bHasCachedData[(int)EGame.MiniCactpot] = true;
                        }
                    }
                }
            }
        }

        private void DoWorkScanTripleTriad(EMode mode, Stopwatch perfTimer, FastBitmapHSV fastBitmap, string imagePath)
        {
            if ((mode & EMode.Rules) != EMode.None)
            {
                ParseRules(fastBitmap, cachedRuleBox, currentTriadGame.mods);
            }

            bool bCanContinue = (currentState != EState.UnknownHash) || ((mode & EMode.Debug) != EMode.None);
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
                    perfTimer.Restart();

                    bool[] greyedOutBlue = new bool[5];
                    int numGreyedOutBlue = 0;
                    for (int Idx = 0; Idx < 5; Idx++)
                    {
                        currentTriadGame.blueDeck[Idx] = ParseCard(fastBitmap, cachedBlueCards[Idx], listDigits, "blue" + Idx, out greyedOutBlue[Idx]);
                        currentTriadGame.redDeck[Idx] = ParseCard(fastBitmap, cachedRedCards[Idx], listDigits, "red" + Idx, out bool dummyFlag);
                        numGreyedOutBlue += greyedOutBlue[Idx] ? 1 : 0;
                        bHasFailedCardMatch = bHasFailedCardMatch || (currentTriadGame.blueDeck[Idx] == failedMatchCard) || (currentTriadGame.redDeck[Idx] == failedMatchCard);
                    }

                    currentTriadGame.forcedBlueCard = null;
                    if (numGreyedOutBlue > 0)
                    {
                        for (int Idx = 0; Idx < 5; Idx++)
                        {
                            if (currentTriadGame.blueDeck[Idx] != null && !greyedOutBlue[Idx])
                            {
                                currentTriadGame.forcedBlueCard = currentTriadGame.blueDeck[Idx];
                            }
                        }
                    }

                    perfTimer.Stop();
                    if (bDebugMode) { Logger.WriteLine("Parse decks: " + perfTimer.ElapsedMilliseconds + "ms"); }
                }

                if ((mode & EMode.Board) != EMode.None)
                {
                    perfTimer.Restart();

                    for (int Idx = 0; Idx < 9; Idx++)
                    {
                        currentTriadGame.board[Idx] = ParseCard(fastBitmap, cachedBoardCards[Idx], listDigits, "board" + Idx, out bool dummyFlag);
                        bHasFailedCardMatch = bHasFailedCardMatch || (currentTriadGame.board[Idx] == failedMatchCard);

                        if (currentTriadGame.board[Idx] != null)
                        {
                            int gridCellLeft = cachedGridBox.Left + ((Idx % 3) * cachedGridBox.Width / 3);
                            currentTriadGame.boardOwner[Idx] = ParseCardOwner(fastBitmap, cachedBoardCards[Idx], gridCellLeft, "board" + Idx);
                        }
                    }

                    perfTimer.Stop();
                    if (bDebugMode) { Logger.WriteLine("Parse board: " + perfTimer.ElapsedMilliseconds + "ms"); }
                }

                if (bHasFailedCardMatch)
                {
                    if (currentState != EState.FailedCardMatching && Logger.IsActive() && !bDebugMode)
                    {
                        string screenshotName = "screenshot_failed_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        cachedScreenshot.Save(imagePath + screenshotName + ".jpg");

                        List<Rectangle> debugBounds = new List<Rectangle>();
                        if (cachedGridBox.Width > 0) { debugBounds.Add(cachedGridBox); }
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

        private void DoWorkScanMiniCactpot(EMode mode, Stopwatch perfTimer, FastBitmapHSV fastBitmaph)
        {
            if ((mode & EMode.Cactpot) != EMode.None)
            {
                perfTimer.Restart();
                currentState = EState.NoErrors;

                currentCactpotGame.numRevealed = 0;
                for (int Idx = 0; Idx < currentCactpotGame.board.Length; Idx++)
                {
                    currentCactpotGame.board[Idx] = ParseCactpotCircle(fastBitmaph, cachedCactpotCircles[Idx], "board" + Idx);
                    currentCactpotGame.numRevealed += (currentCactpotGame.board[Idx] != 0) ? 1 : 0;
                }

                perfTimer.Stop();
                if (bDebugMode) { Logger.WriteLine("Parse cactpot board: " + perfTimer.ElapsedMilliseconds + "ms"); }
            }
        }

        public EState GetCurrentState()
        {
            return currentState;
        }

        public ETurnState GetCurrentTurnState()
        {
            return currentTurnState;
        }

        public EGame GetCurrentGameType()
        {
            return currentGameType;
        }

        public Rectangle GetGameWindowRect()
        {
            return cachedGameWindow;
        }

        public Rectangle GetGridRect()
        {
            return cachedGridBox;
        }

        public Rectangle GetRuleBoxRect()
        {
            return cachedRuleBox;
        }

        public Rectangle GetCactpotBoardRect()
        {
            return cachedCactpotBox;
        }

        public Rectangle GetBlueCardRect(int Idx)
        {
            return cachedBlueCards[Idx];
        }

        public Rectangle GetBoardCardRect(int Idx)
        {
            return cachedBoardCards[Idx];
        }

        public Rectangle GetCactpotCircleBox(int Idx)
        {
            return cachedCactpotCircles[Idx];
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

        #region Screenshot

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string DeviceName, int ModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner

            public override string ToString()
            {
                return string.Format("[L:{0},T:{1},R:{2},B:{3}]", Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        private HandleRef FindGameWindow()
        {
            HandleRef WindowHandle = new HandleRef();
            bool useVerboseLogs = Logger.IsSuperVerbose();

            if (cachedProcess == null || !cachedProcess.MainWindowTitle.StartsWith("FINAL FANTASY"))
            {
                Process[] processes = Process.GetProcessesByName("ffxiv_dx11");
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName("ffxiv");
                }

                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: process list to check: " + processes.Length); }

                cachedProcess = null;
                foreach (Process p in processes)
                {
                    bool hasMatchingTitle = p.MainWindowTitle.StartsWith("FINAL FANTASY");
                    if (useVerboseLogs)
                    {
                        Logger.WriteLine(">> pid:" + p.Id + ", name:" + p.ProcessName +
                            "', window:'" + p.MainWindowTitle +
                            "', hwnd:0x" + p.MainWindowHandle.ToInt64().ToString("x") +
                            " => " + (hasMatchingTitle ? "match!" : "nope"));

                        try { Logger.WriteLine("   path:'" + p.MainModule.FileName + "'"); }
                        catch (Exception ex) { Logger.WriteLine("   path: FAILED: " + ex); }
                    }

                    if (hasMatchingTitle)
                    {
                        cachedProcess = p;
                        break;
                    }
                }
            }

            if (cachedProcess != null)
            {
                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: 0x" + cachedProcess.MainWindowHandle.ToInt64().ToString("x")); }
                WindowHandle = new HandleRef(this, cachedProcess.MainWindowHandle);
            }
            else
            {
                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: can't find window!"); }
                currentState = EState.MissingGameProcess;
            }

            return WindowHandle;
        }

        public Rectangle FindGameWindowBounds()
        {
            HandleRef windowHandle = FindGameWindow();
            Rectangle result = GetGameWindowBoundsFromAPI(windowHandle);
            return result;
        }

        private Rectangle GetGameWindowBoundsFromAPI(HandleRef windowHandle)
        {
            Rectangle result = new Rectangle(0, 0, 0, 0);

            bool bHasWindow = windowHandle.Handle.ToInt64() != 0;
            if (bHasWindow)
            {
                if (GetWindowRect(windowHandle, out RECT windowRectApi))
                {
                    result = new Rectangle(windowRectApi.Left, windowRectApi.Top, windowRectApi.Right - windowRectApi.Left, windowRectApi.Bottom - windowRectApi.Top);
                    if (Logger.IsSuperVerbose()) { Logger.WriteLine("GetGameWindowBoundsFromAPI: handle:0x" + windowHandle.Handle.ToInt64().ToString("x") + " bounds: " + result + ", api:" + windowRectApi); }
                }
            }

            return result;
        }

        public Rectangle GetAdjustedGameWindowBounds(HandleRef windowHandle)
        {
            Rectangle result = GetGameWindowBoundsFromAPI(windowHandle);
            if (result.Width > 0 && PlayerSettingsDB.Get().useFullScreenCapture)
            {
                result = Screen.GetBounds(result);
            }

            Screen activeScreen = Screen.FromHandle(windowHandle.Handle);
            if (activeScreen != cachedScreen)
            {
                cachedScreen = activeScreen;
                
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                EnumDisplaySettings(cachedScreen.DeviceName, -1, ref dm);

                if (dm.dmPelsWidth == cachedScreen.Bounds.Width)
                {
                    cachedScreenScaling = 1.0f;
                }
                else
                {
                    cachedScreenScaling = (float)cachedScreen.Bounds.Width / (float)dm.dmPelsWidth;
                }
            }
            
            if (cachedScreenScaling != 1.0f)
            {
                result.X = (int)(result.X / cachedScreenScaling);
                result.Y = (int)(result.Y / cachedScreenScaling);
                result.Width = (int)(result.Width / cachedScreenScaling);
                result.Height = (int)(result.Height / cachedScreenScaling);
            }

            return result;
        }

        private Bitmap TakeScreenshot(HandleRef windowHandle)
        {
            bool useVerboseLogs = Logger.IsSuperVerbose();
            Bitmap bitmap = null;

            Rectangle bounds = GetAdjustedGameWindowBounds(windowHandle);
            if (bounds.Width > 0)
            {
                cachedGameWindow = bounds;
                if (useVerboseLogs) { Logger.WriteLine("TakeScreenshot: bounds " + cachedGameWindow); }

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
                        if (useVerboseLogs) { Logger.WriteLine(">> copied from screen"); }
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
                        PrintWindow(windowHandle.Handle, hdcBitmap, 0);
                        g.ReleaseHdc(hdcBitmap);
                        if (useVerboseLogs) { Logger.WriteLine(">> captured content"); }
                    }
                }
            }
            else
            {
                currentState = EState.MissingGameWindow;
            }

            return bitmap;
        }

        private Bitmap TakeScreenshotPartial(HandleRef windowHandle, Rectangle innerBounds)
        {
            bool useVerboseLogs = Logger.IsSuperVerbose();
            Bitmap bitmap = null;

            Rectangle bounds = GetAdjustedGameWindowBounds(windowHandle);
            if (bounds.Width > 0 && innerBounds.Width > 0)
            {
                if (useVerboseLogs) { Logger.WriteLine("TakeScreenshotPartial: bounds " + bounds); }

                bitmap = new Bitmap(innerBounds.Width, innerBounds.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // copy entire screen - will capture all windows on top of game too
                    Point copyPt = new Point(bounds.Left + innerBounds.Left, bounds.Top + innerBounds.Top);
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

        #endregion

        #region TripleTriad

        private Rectangle FindGridCoords(FastBitmapHSV bitmap)
        {
            Rectangle GridRect = new Rectangle();

            // detect intersections of grid lines by color match around that point
            //
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
            if (Logger.IsSuperVerbose()) { Logger.WriteLine("FindGridCoords: " + GridRect); }

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

            // failsafe: assume predefined location
            // may not be accurate, but it's much better than having no rules scanned
            if (RuleBoxRect.Width <= 0)
            {
                RuleBoxRect = new Rectangle(
                    gridRect.Left + (gridRect.Width * 113 / 100),
                    gridRect.Top - (gridRect.Height * 5 / 100),
                    gridRect.Width * 66 / 100,
                    gridRect.Height * 20 / 100
                    );
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("FindRuleBoxCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }
            if (Logger.IsSuperVerbose()) { Logger.WriteLine("FindRuleBoxCoords: " + RuleBoxRect); }

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

            if (Logger.IsSuperVerbose()) { Logger.WriteLine("FindTimerBox: " + result); }
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

            if (Logger.IsSuperVerbose()) { Logger.WriteLine("FindTimerBox: " + result); }
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
            if (Logger.IsSuperVerbose())
            {
                string desc = "FindBuleCardCoords: ";
                foreach (var r in result) { desc += r.ToString() + " "; }
                Logger.WriteLine(desc);
            }

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
                        bool isValid =
                            (ruleHashData.ContextBounds.Top >= 0) &&
                            (ruleHashData.ContextBounds.Left >= 0) &&
                            (ruleHashData.ContextBounds.Bottom < cachedScreenshot.Height) &&
                            (ruleHashData.ContextBounds.Right < cachedScreenshot.Width);

                        if (isValid)
                        {
                            ScreenshotUtilities.ConditionalAddUnknownHash(
                                ScreenshotUtilities.CreateUnknownImageHash(ruleHashData, cachedScreenshot, EImageHashType.Rule), unknownHashes);

                            currentState = EState.UnknownHash;
                        }
                        else
                        {
                            Logger.WriteLine("ParseRules ERROR: out of bounds! screenshot:" + cachedScreenshot.Width + "x" + cachedScreenshot.Height + ", hashContext:" + ruleHashData.ContextBounds + ", ruleBox:" + rulesRect + ", fastBitmap:" + bitmap.Width + "x" + bitmap.Height);
                        }
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

            CardState detectionState = new CardState();
            detectionState.sideImage = new ImageDataDigit[4];
            int[] CardNumbers = new int[4] { 0, 0, 0, 0 };

            // number match tries to avoid scaling and hopes that 100% UI scale will create digits fitting 8x10 boxes
            // check if numberbox height is roughly (2x digit height + 2x offset:2) = 20
            // - yes: yay, go and match pixel perfect digits
            // - nope: assume digit box with height 50% of number box and width to keep 8x10 ratio
            int heightCheckError = numberBox.Height - (ImagePatternDigit.Height * 2) - 4;
            if (heightCheckError < 5)
            {                  
                Point[] cardScanAnchors = new Point[4]
                {
                    new Point(numberBoxMidX - (approxCardWidth / 2), numberBox.Top),
                    new Point(numberBoxMidX - (approxCardWidth / 2), numberBox.Bottom - approxCardHeight),
                    new Point(numberBoxMidX + (approxCardWidth * 40 / 100), numberBoxMidY - (approxCardHeight / 2)),
                    new Point(numberBoxMidX - (approxCardWidth * 110 / 100), numberBoxMidY - (approxCardHeight / 2)),
                };

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    CardNumbers[Idx] = ScreenshotUtilities.FindPatternMatch(bitmap, cardScanAnchors[Idx], colorMatchNumAdjusted, digitList, out detectionState.sideImage[Idx]);
                }
            }
            else
            {
                int sadDigitHeight = numberBox.Height * 45 / 100;
                int sadDigitWidth = sadDigitHeight * ImagePatternDigit.Width / ImagePatternDigit.Height;
                Size sadDigitSize = new Size(sadDigitWidth, sadDigitHeight);

                Point[] cardScanAnchors = new Point[4]
                {
                    new Point(numberBoxMidX - (sadDigitWidth / 2), numberBox.Top),
                    new Point(numberBoxMidX - (sadDigitWidth / 2), numberBox.Bottom - sadDigitHeight),
                    new Point(numberBoxMidX + (sadDigitWidth / 2) + 2, numberBoxMidY - (sadDigitHeight / 2)),
                    new Point(numberBoxMidX - (sadDigitWidth * 3 / 2) - 2, numberBoxMidY - (sadDigitHeight / 2)),
                };

                const int inactiveThr = 220 * 80 / 100;
                colorMatchNumAdjusted = new FastPixelMatchMono((byte)(maxMono * 80 / 100), 255);
                bIsGreyedOut = (maxMono < inactiveThr);

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    CardNumbers[Idx] = ScreenshotUtilities.FindPatternMatchScaled(bitmap, cardScanAnchors[Idx], sadDigitSize, colorMatchNumAdjusted, digitList, out detectionState.sideImage[Idx]);
                    debugShapes.Add(new Rectangle(cardScanAnchors[Idx], sadDigitSize));
                }
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

        private ETriadCardOwner ParseCardOwner(FastBitmapHSV bitmap, Rectangle cardRect, int gridCellLeft, string debugName)
        {
            ETriadCardOwner owner = ETriadCardOwner.Unknown;

            int testWidth = 10;
            int testHeight = cardRect.Height / 3;

            Rectangle testBounds = new Rectangle(gridCellLeft + 5, cardRect.Top + ((cardRect.Height - testHeight) / 2), Math.Min(testWidth, cardRect.Left - gridCellLeft - 10), testHeight);
            if (bDebugMode) { debugShapes.Add(testBounds); }

            int counterRed = 0;
            int counterBlue = 0;

            for (int IdxY = testBounds.Top; IdxY < testBounds.Bottom; IdxY++)
            {
                for (int IdxX = testBounds.Left; IdxX < testBounds.Right; IdxX++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(IdxX, IdxY);
                    counterRed += (colorMatchCardOwnerRed1.IsMatching(testPx) || colorMatchCardOwnerRed2.IsMatching(testPx)) ? 1 : 0;
                    counterBlue += colorMatchCardOwnerBlue.IsMatching(testPx) ? 1 : 0;
                }
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

        #endregion

        #region MiniCactpot

        private Rectangle FindCactpotCoords(FastBitmapHSV bitmap)
        {
            Rectangle rect = new Rectangle();

            // find number fields
            //
            // scan method similar to triple triad board with matching top left corner and tile size

            int minCellSize = bitmap.Width * 2 / 100;
            int maxCellSize = bitmap.Width * 6 / 100;
            int maxScanX = bitmap.Width - (maxCellSize * 3) - 20;
            int maxScanY = bitmap.Height - (maxCellSize * 2) - 20;

            //HasCactpotCircleMatch(bitmap, 580, 469, 54, true);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool bScanning = true;
            for (int IdxY = 0; IdxY < maxScanY && bScanning; IdxY++)
            {
                int ScanY = (bitmap.Height / 2) + ((IdxY / 2) * ((IdxY % 2 == 0) ? 1 : -1));
                for (int IdxX = 10; IdxX < maxScanX && bScanning; IdxX++)
                {
                    if (HasCactpotCircleEdgeV(bitmap, IdxX, ScanY, -1))
                    {
                        for (int cellSize = minCellSize; cellSize < maxCellSize && bScanning; cellSize++)
                        {
                            if (HasCactpotCellMatch(bitmap, IdxX, ScanY, cellSize, out int CellPosX, out int CellPosY))
                            {
                                if (HasCactpotCircleMatch(bitmap, CellPosX, CellPosY, cellSize))
                                {
                                    if (HasCactpotMatch(bitmap, CellPosX, CellPosY, cellSize))
                                    {
                                        rect = new Rectangle(CellPosX, CellPosY, cellSize * 3, cellSize * 3);
                                        bScanning = false;
                                        break;
                                    }
                                    else
                                    {
                                        // skip all nearby matches on X, will still hit all nearby on Y
                                        IdxX += 3;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();
            if (bDebugMode) { Logger.WriteLine("FindCactpotCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }

            return rect;
        }

        private bool HasCactpotCircleMatch(FastBitmapHSV bitmap, int posX, int posY, int cellSize, bool bDebugDetection = false)
        {
            int sizeA = cellSize;
            int offsetB = 5;
            int sizeB = sizeA - (offsetB * 2);

            Point[] testPoints = new Point[4];

            // 4 points: corners of cell => dark background
            {
                testPoints[0].X = posX; testPoints[0].Y = posY;
                testPoints[1].X = posX + sizeA; testPoints[1].Y = posY;
                testPoints[2].X = posX; testPoints[2].Y = posY + sizeA;
                testPoints[3].X = posX + sizeA; testPoints[3].Y = posY + sizeA;

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(testPoints[Idx].X, testPoints[Idx].Y);
                    //if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] testing A[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }

                    if (!colorMatchCactpotBack.IsMatching(testPx))
                    {
                        if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] failed, not background: A[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }
                        return false;
                    }
                }
            }

            // 4 points: corners with offset B => dark background
            {
                testPoints[0].X = posX + offsetB; testPoints[0].Y = posY + offsetB;
                testPoints[1].X = posX + sizeA - offsetB; testPoints[1].Y = posY + offsetB;
                testPoints[2].X = posX + offsetB; testPoints[2].Y = posY + sizeA - offsetB;
                testPoints[3].X = posX + sizeA - offsetB; testPoints[3].Y = posY + sizeA - offsetB;

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(testPoints[Idx].X, testPoints[Idx].Y);
                    //if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] testing B1[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }

                    if (!colorMatchCactpotBack.IsMatching(testPx))
                    {
                        if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] failed, not background: B1[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }
                        return false;
                    }
                }
            }

            // 4 points: midpoints with offset B => yellow/white
            {
                testPoints[0].X = posX + offsetB; testPoints[0].Y = posY + (sizeA / 2);
                testPoints[1].X = posX + sizeA - offsetB; testPoints[1].Y = posY + (sizeA / 2);
                testPoints[2].X = posX + (sizeA / 2); testPoints[2].Y = posY + offsetB;
                testPoints[3].X = posX + (sizeA / 2); testPoints[3].Y = posY + sizeA - offsetB;

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(testPoints[Idx].X, testPoints[Idx].Y);
                    //if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] testing B2[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }

                    if (!colorMatchCactpotCircleOut.IsMatching(testPx))
                    {
                        if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] failed, not circle: B2[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }
                        return false;
                    }
                }
            }

            // 4 points: midpoints of diagonal lines between midpoints B (C) => yellow/white 
            {
                testPoints[0].X = posX + offsetB + (sizeB / 4); testPoints[0].Y = posY + offsetB + (sizeB / 4);
                testPoints[1].X = posX + sizeA - offsetB - (sizeB / 4); testPoints[1].Y = posY + offsetB + (sizeB / 4);
                testPoints[2].X = posX + offsetB + (sizeB / 4); testPoints[2].Y = posY + sizeA - offsetB - (sizeB / 4);
                testPoints[3].X = posX + sizeA - offsetB - (sizeB / 4); testPoints[3].Y = posY + sizeA - offsetB - (sizeB / 4);

                for (int Idx = 0; Idx < 4; Idx++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(testPoints[Idx].X, testPoints[Idx].Y);
                    //if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] testing B3[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }

                    if (!colorMatchCactpotCircleIn.IsMatching(testPx))
                    {
                        if (bDebugDetection) { Logger.WriteLine("[" + posX + ", " + posY + "] failed, not circle: B3[" + testPoints[Idx].X + "," + testPoints[Idx].Y + "]: " + testPx); }
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HasCactpotMatch(FastBitmapHSV bitmap, int posX, int posY, int cellSize, bool bDebugDetection = false)
        {
            if (bDebugDetection) { Logger.WriteLine("HasCactpotMatch[" + posX + ", " + posY + "]? testing..."); }

            // circle at (posX, posY) already matched, test other 8
            bool bHasMatch = true;
            bool bHasMatch01 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + cellSize, posY, cellSize);
            bHasMatch = bHasMatch && bHasMatch01;
            bool bHasMatch02 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + (cellSize * 2), posY, cellSize);
            bHasMatch = bHasMatch && bHasMatch02;

            bool bHasMatch10 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX, posY + cellSize, cellSize);
            bHasMatch = bHasMatch && bHasMatch10;
            bool bHasMatch11 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + cellSize, posY + cellSize, cellSize);
            bHasMatch = bHasMatch && bHasMatch11;
            bool bHasMatch12 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + (cellSize * 2), posY + cellSize, cellSize);
            bHasMatch = bHasMatch && bHasMatch12;

            bool bHasMatch20 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX, posY + (cellSize * 2), cellSize);
            bHasMatch = bHasMatch && bHasMatch20;
            bool bHasMatch21 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + cellSize, posY + (cellSize * 2), cellSize);
            bHasMatch = bHasMatch && bHasMatch21;
            bool bHasMatch22 = (bHasMatch || bDebugDetection) && HasCactpotCircleMatch(bitmap, posX + (cellSize * 2), posY + (cellSize * 2), cellSize);
            bHasMatch = bHasMatch && bHasMatch22;

            if (bDebugDetection)
            {
                Logger.WriteLine(">> [#" + (bHasMatch01 ? "#" : ".") + (bHasMatch02 ? "#" : ".") +
                    "][" + (bHasMatch10 ? "#" : ".") + (bHasMatch11 ? "#" : ".") + (bHasMatch12 ? "#" : ".") +
                    "][" + (bHasMatch20 ? "#" : ".") + (bHasMatch21 ? "#" : ".") + (bHasMatch22 ? "#" : ".") +
                    "] => " + (bHasMatch ? "match" : "nope"));
            }

            return bHasMatch;
        }

        private bool HasCactpotCircleEdgeV(FastBitmapHSV bitmap, int posX, int posY, int sideX, bool bDebugDetection = false)
        {
            const int spreadY = 2;
            const int offsetDeepX = 5;

            FastPixelHSV testPx = bitmap.GetPixel(posX, posY);
            if (!colorMatchCactpotCircleOut.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge center " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX - (sideX * offsetDeepX), posY);
            if (!colorMatchCactpotCircleOut.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: deep center " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX, posY - spreadY);
            if (!colorMatchCactpotCircleOut.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge top " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX, posY + spreadY);
            if (!colorMatchCactpotCircleOut.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge bottom " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX + sideX, posY);
            if (!colorMatchCactpotBack.IsMatching(testPx) && !colorMatchCactpotCircleFade.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge center prev " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX + sideX, posY - spreadY);
            if (!colorMatchCactpotBack.IsMatching(testPx) && !colorMatchCactpotCircleFade.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge top prev " + testPx); }
                return false;
            }

            testPx = bitmap.GetPixel(posX + sideX, posY + spreadY);
            if (!colorMatchCactpotBack.IsMatching(testPx) && !colorMatchCactpotCircleFade.IsMatching(testPx))
            {
                if (bDebugDetection) { Logger.WriteLine("HasCactpotCircleEdgeV[" + posX + "," + posY + "] failed: edge bottom prev " + testPx); }
                return false;
            }

            return true;
        }

        private bool HasCactpotCellMatch(FastBitmapHSV bitmap, int posX, int posY, int cellSize, out int cellPosX, out int cellPosY, bool bDebugDetection = false)
        {
            cellPosX = 0;
            cellPosY = 0;

            bool bHasEdgeV2 = HasCactpotCircleEdgeV(bitmap, posX + cellSize, posY, -1);
            if (bDebugDetection) { Logger.WriteLine("HasCactpotCellMatch[" + posX + ", " + posY + "], cellSize:" + cellSize + " => " + (bHasEdgeV2 ? "found V2" : "nope")); }

            if (bHasEdgeV2)
            {
                for (int Idx = 5; Idx < 20; Idx++)
                {
                    int invPosX = posX + cellSize - Idx;
                    bool bHasEdgeV3 = HasCactpotCircleEdgeV(bitmap, invPosX, posY, 1);
                    if (bHasEdgeV3)
                    {
                        if (bDebugDetection) { Logger.WriteLine(">> spacing check: V3 at X:" + invPosX); }

                        cellPosX = posX - (Idx / 2);
                        cellPosY = posY - (cellSize / 2);

                        return true;
                    }
                }
            }

            return false;
        }

        private Rectangle[] FindCactpotCircleCoords(Rectangle cactpotBox)
        {
            Rectangle[] circleBoxes = new Rectangle[9];
            int cellSize = cactpotBox.Width / 3;

            for (int IdxY = 0; IdxY < 3; IdxY++)
            {
                for (int IdxX = 0; IdxX < 3; IdxX++)
                {
                    circleBoxes[IdxX + (IdxY * 3)] = new Rectangle(cactpotBox.Left + (IdxX * cellSize), cactpotBox.Top + (IdxY * cellSize), cellSize, cellSize);
                }
            }

            return circleBoxes;
        }

        private int ParseCactpotCircle(FastBitmapHSV bitmap, Rectangle circleBox, string debugName)
        {
            int result = 0;

            int innerOffsetX = circleBox.Width / 3;
            int innerOffsetY = circleBox.Height / 4;
            Rectangle numberBox = new Rectangle(circleBox.Left + innerOffsetX, circleBox.Top + innerOffsetY, circleBox.Width - (innerOffsetX * 2), circleBox.Height - (innerOffsetY * 2));
            if (bDebugMode) { debugShapes.Add(numberBox); }

            float numberPct = ScreenshotUtilities.CountFillPct(bitmap, numberBox, colorMatchCactpotNumber);
            if (numberPct > 0.05f)
            {
                if (bDebugMode) { Logger.WriteLine("ParseCactpotCircle[" + debugName + "] has number inside"); }

                List<Point> spanV = ScreenshotUtilities.TraceSpansV(bitmap, numberBox, colorMatchCactpotNumber, 5);
                foreach (Point span in spanV)
                {
                    Point boundsH = ScreenshotUtilities.TraceBoundsH(bitmap, new Rectangle(numberBox.Left, span.X, numberBox.Width, span.Y), colorMatchCactpotNumber, span.Y * 2);
                    if (boundsH.Y >= 4)
                    {
                        Rectangle hashBox = new Rectangle(boundsH.X, span.X, boundsH.Y, span.Y);

                        FastBitmapHash cactpotHashData = ScreenshotUtilities.CalculateImageHash(bitmap, hashBox, circleBox, colorMatchCactpotNumber, 8, 16, true);

                        CactpotNumberHash bestNumberOb = (CactpotNumberHash)ScreenshotUtilities.FindMatchingHash(cactpotHashData, EImageHashType.Cactpot, out int bestDistance, bDebugMode);
                        if (bestNumberOb == null)
                        {
                            ScreenshotUtilities.ConditionalAddUnknownHash(
                                ScreenshotUtilities.CreateUnknownImageHash(cactpotHashData, cachedScreenshot, EImageHashType.Cactpot), unknownHashes);

                            currentState = EState.UnknownHash;
                        }
                        else
                        {
                            cactpotHashData.GuideOb = bestNumberOb;
                            currentHashDetections.Add(cactpotHashData, bestDistance);
                            result = bestNumberOb.number;
                        }

                        debugHashes.Add(cactpotHashData);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}