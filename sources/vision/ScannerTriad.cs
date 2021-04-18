using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace FFTriadBuddy
{
    public class ScannerTriad : ScannerBase
    {
        public enum ETurnState
        {
            MissingTimer,
            Waiting,
            Active,
        }

        public class GameState : GameStateBase
        {
            public TriadCard[] board;
            public ETriadCardOwner[] boardOwner;
            public TriadCard[] blueDeck;
            public TriadCard[] redDeck;
            public TriadCard forcedBlueCard;
            public List<TriadGameModifier> mods;
            public ETurnState turnState;

            public GameState()
            {
                board = new TriadCard[9];
                boardOwner = new ETriadCardOwner[9];
                blueDeck = new TriadCard[5];
                redDeck = new TriadCard[5];
                forcedBlueCard = null;
                mods = new List<TriadGameModifier>();
                turnState = ETurnState.MissingTimer;
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

        [Flags]
        enum ESide
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8,
        }

        [Flags]
        public enum EScanMode
        {
            Default = 0,
            TimerOnly = 1,
        }

        public enum EScanError
        {
            NoErrors,
            Aborted,
            MissingGrid,
            MissingCards,
            FailedCardMatching,
        }

        private Rectangle cachedGridBox;
        private Rectangle cachedRuleBox;
        private Rectangle cachedTimerBox;
        private Rectangle cachedTimerScanBox;
        private Rectangle[] cachedBlueCards;
        private Rectangle[] cachedRedCards;
        private Rectangle[] cachedBoardCards;
        private List<ImagePatternDigit> digitMasks = new List<ImagePatternDigit>();
        private TriadCard failedMatchCard = new TriadCard(-1, "failedMatch", "", ETriadCardRarity.Common, ETriadCardType.None, 0, 0, 0, 0, 0, 0);

        public GameState cachedGameState;
        public List<CardState> cachedCardState = new List<CardState>();
        public EScanError cachedScanError = EScanError.NoErrors;

        private FastPixelMatch colorMatchGridBorder = new FastPixelMatchMono(0, 150);
        private FastPixelMatch colorMatchGridField = new FastPixelMatchMono(170, 255);
        private FastPixelMatch colorMatchRuleBox = new FastPixelMatchMono(20, 80);
        private FastPixelMatch colorMatchRuleText = new FastPixelMatchMono(150, 255);
        private FastPixelMatch colorMatchCardBorder = new FastPixelMatchHSV(20, 40, 0, 100, 0, 100);
        private FastPixelMatch colorMatchCardNumber = new FastPixelMatchMono(220, 255);
        private FastPixelMatch colorMatchCardOwnerRed1 = new FastPixelMatchHueMono(0, 30, 150, 255);
        private FastPixelMatch colorMatchCardOwnerRed2 = new FastPixelMatchHueMono(330, 360, 150, 255);
        private FastPixelMatch colorMatchCardOwnerBlue = new FastPixelMatchHueMono(200, 280, 150, 255);
        private FastPixelMatch colorMatchTimerBox = new FastPixelMatchHSV(40, 60, 10, 40, 0, 100);
        private FastPixelMatch colorMatchTimerActive = new FastPixelMatchMono(80, 255);

        public ScannerTriad()
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

        public override void InvalidateCache()
        {
            cachedGridBox = Rectangle.Empty;
            cachedBlueCards = null;
            cachedRedCards = null;
            cachedBoardCards = null;
            cachedGameState = null;
        }

        public override bool HasValidCache(FastBitmapHSV bitmap, int scannerFlags)
        {
            if ((scannerFlags & (int)EScanMode.TimerOnly) != 0)
            {
                return true;
            }

            return cachedGridBox.Width > 0 && HasGridMatch(bitmap, cachedGridBox.Left, cachedGridBox.Top, cachedGridBox.Width / 3);
        }

        public Rectangle GetBoardBox() { return cachedGridBox; }
        public Rectangle GetRuleBox() { return cachedRuleBox; }
        public Rectangle GetTimerScanBox() { return cachedTimerScanBox; }
        public Rectangle GetBlueCardBox(int idx) { return cachedBlueCards[idx]; }
        public Rectangle GetBoardCardBox(int idx) { return cachedBoardCards[idx]; }

        public override void AppendDebugShapes(List<Rectangle> shapes)
        {
            base.AppendDebugShapes(shapes);
            if (cachedGridBox.Width > 0) { shapes.Add(cachedGridBox); }
            if (cachedRuleBox.Width > 0) { shapes.Add(cachedRuleBox); }
            if (cachedTimerBox.Width > 0) { shapes.Add(cachedTimerBox); }
            if (cachedBlueCards != null) { shapes.AddRange(cachedBlueCards); }
            if (cachedRedCards != null) { shapes.AddRange(cachedRedCards); }
            if (cachedBoardCards != null) { shapes.AddRange(cachedBoardCards); }
        }

        public override bool DoWork(FastBitmapHSV bitmap, int scannerFlags, Stopwatch perfTimer, bool debugMode)
        {
            base.DoWork(bitmap, scannerFlags, perfTimer, debugMode);
            perfTimer.Restart();

            bool scanResult = true;
            cachedScanError = EScanError.NoErrors;

            bool isTimerOnly = (scannerFlags & (int)EScanMode.TimerOnly) != 0;
            if (isTimerOnly)
            {
                if (cachedGameState != null)
                {
                    Rectangle fastTimerBox = (cachedTimerScanBox.Width > 0) ? FindTimerBox(bitmap, cachedTimerScanBox) : Rectangle.Empty;
                    if (fastTimerBox.Width > 0)
                    {
                        debugShapes.Add(fastTimerBox);

                        bool bHasActiveTimer = ParseTimer(bitmap, fastTimerBox);
                        cachedGameState.turnState = bHasActiveTimer ? ETurnState.Active : ETurnState.Waiting;
                    }
                    else
                    {
                        cachedGameState.turnState = ETurnState.MissingTimer;
                    }
                }
            }
            else
            {
                cachedScanError = EScanError.MissingGrid;

                if (!HasValidCache(bitmap, scannerFlags))
                {
                    cachedBlueCards = null;
                    cachedRedCards = null;
                    cachedBoardCards = null;

                    cachedGridBox = FindGridCoords(bitmap);
                    if (cachedGridBox.Width > 0)
                    {
                        cachedRuleBox = FindRuleBoxCoords(bitmap, cachedGridBox);
                        cachedTimerBox = FindTimerBox(bitmap, cachedGridBox, cachedRuleBox);
                        cachedTimerScanBox = new Rectangle(cachedTimerBox.X, cachedTimerBox.Y - 20, cachedTimerBox.Width, cachedTimerBox.Height + 40);
                        Rectangle scanAreaBox = new Rectangle(cachedGridBox.Left - (cachedGridBox.Width * 85 / 100),
                            cachedGridBox.Top - (cachedGridBox.Height * 5 / 100),
                            cachedGridBox.Width * 270 / 100,
                            cachedGridBox.Height * 110 / 100);

                        cachedBlueCards = FindBlueCardCoords(bitmap, cachedGridBox);
                        if (cachedBlueCards != null && cachedBlueCards.Length == 5)
                        {
                            cachedRedCards = FindRedCardCoords(cachedGridBox, cachedBlueCards);
                            cachedBoardCards = FindBoardCardCoords(cachedGridBox, cachedBlueCards);
                            screenAnalyzer.currentScanArea = scanAreaBox;

                            // mark as aborted, will be overwritten when completed
                            cachedScanError = EScanError.Aborted;
                        }
                        else
                        {
                            cachedScanError = EScanError.MissingCards;
                        }
                    }
                    else
                    {
                        scanResult = false;
                    }
                }

                cachedGameState = null;
                if (cachedBoardCards != null)
                {
                    cachedGameState = new GameState();
                    cachedGameStateBase = cachedGameState;

                    ParseRules(bitmap, cachedRuleBox, cachedGameState.mods);

                    bool bCanContinue = (screenAnalyzer.GetCurrentState() != ScreenAnalyzer.EState.UnknownHash) || debugMode;
                    if (bCanContinue)
                    {
                        bool bHasFailedCardMatch = false;

                        cachedScanError = EScanError.NoErrors;
                        cachedCardState.Clear();

                        List<ImagePatternDigit> listDigits = new List<ImagePatternDigit>();
                        listDigits.AddRange(digitMasks);
                        listDigits.AddRange(PlayerSettingsDB.Get().customDigits);

                        {
                            perfTimer.Restart();

                            bool[] greyedOutBlue = new bool[5];
                            int numGreyedOutBlue = 0;
                            for (int Idx = 0; Idx < 5; Idx++)
                            {
                                cachedGameState.blueDeck[Idx] = ParseCard(bitmap, cachedBlueCards[Idx], listDigits, "blue" + Idx, out greyedOutBlue[Idx]);
                                cachedGameState.redDeck[Idx] = ParseCard(bitmap, cachedRedCards[Idx], listDigits, "red" + Idx, out bool dummyFlag);
                                numGreyedOutBlue += greyedOutBlue[Idx] ? 1 : 0;
                                bHasFailedCardMatch = bHasFailedCardMatch || (cachedGameState.blueDeck[Idx] == failedMatchCard) || (cachedGameState.redDeck[Idx] == failedMatchCard);
                            }

                            cachedGameState.forcedBlueCard = null;
                            if (numGreyedOutBlue > 0)
                            {
                                for (int Idx = 0; Idx < 5; Idx++)
                                {
                                    if (cachedGameState.blueDeck[Idx] != null && !greyedOutBlue[Idx])
                                    {
                                        cachedGameState.forcedBlueCard = cachedGameState.blueDeck[Idx];
                                    }
                                }
                            }

                            perfTimer.Stop();
                            if (debugMode) { Logger.WriteLine("Parse decks: " + perfTimer.ElapsedMilliseconds + "ms"); }
                        }

                        {
                            perfTimer.Restart();

                            for (int Idx = 0; Idx < 9; Idx++)
                            {
                                cachedGameState.board[Idx] = ParseCard(bitmap, cachedBoardCards[Idx], listDigits, "board" + Idx, out bool dummyFlag);
                                bHasFailedCardMatch = bHasFailedCardMatch || (cachedGameState.board[Idx] == failedMatchCard);

                                if (cachedGameState.board[Idx] != null)
                                {
                                    int gridCellLeft = cachedGridBox.Left + ((Idx % 3) * cachedGridBox.Width / 3);
                                    cachedGameState.boardOwner[Idx] = ParseCardOwner(bitmap, cachedBoardCards[Idx], gridCellLeft, "board" + Idx);
                                }
                            }

                            perfTimer.Stop();
                            if (debugMode) { Logger.WriteLine("Parse board: " + perfTimer.ElapsedMilliseconds + "ms"); }
                        }

                        if (bHasFailedCardMatch)
                        {
                            cachedScanError = EScanError.FailedCardMatching;
                        }
                    }
                }

                // at this point, mini game was recognized, but some error occured - stop checking other scanners
                if (scanResult && cachedScanError != EScanError.NoErrors)
                {
                    screenAnalyzer.OnScannerError();
                }
            }

            perfTimer.Stop();
            if (debugMode) { Logger.WriteLine("Parse triad board: " + perfTimer.ElapsedMilliseconds + "ms"); }
            return scanResult;
        }

        #region Image scan

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
            if (debugMode) { Logger.WriteLine("FindGridCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }
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

                List<int> segPosH = ImageUtils.TraceLineSegments(bitmap, traceStart, traceY, 1, 0, traceEnd - traceStart, colorMatchRuleBox, 20, 2);
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

                    List<int> segPosVT = ImageUtils.TraceLineSegments(bitmap, segPosH[0] + 50, traceY, 0, -1, traceY - traceEndY0, colorMatchRuleBox, 5, 2);
                    if (segPosVT.Count == 2)
                    {
                        int traceEndY1 = gridRect.Top + (gridRect.Height / 6);

                        List<int> segPosVB = ImageUtils.TraceLineSegments(bitmap, segPosH[0] + 50, traceY, 0, 1, traceEndY1 - traceY, colorMatchRuleBox, 5, 2);
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
            if (debugMode) { Logger.WriteLine("FindRuleBoxCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }
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

            bool bHasTop = ImageUtils.TraceLine(bitmap, traceStartX, traceStartY, 0, 1, gridRect.Height / 3, colorMatchTimerBox, out Point hitTop);
            if (bHasTop)
            {
                bool bHasBottom = ImageUtils.TraceLine(bitmap, traceStartX, hitTop.Y + 10, 0, -1, 10, colorMatchTimerBox, out Point hitBottom);
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
            bool bHasTop = ImageUtils.TraceLine(bitmap, traceStartX, 0, 0, 1, timerScanRect.Height * 2 / 3, colorMatchTimerBox, out Point hitTop);
            if (bHasTop)
            {
                bool bHasBottom = ImageUtils.TraceLine(bitmap, traceStartX, hitTop.Y + 10, 0, -1, 10, colorMatchTimerBox, out Point hitBottom);
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
                bool bHasHit0 = ImageUtils.TraceLine(bitmap, scanAnchors[Idx].X, scanAnchors[Idx].Y - (approxCardSize.Height / 2), 0, 1, traceLen, colorMatchCardBorder, out Point hitTop);
                bool bHasHit1 = ImageUtils.TraceLine(bitmap, scanAnchors[Idx].X, scanAnchors[Idx].Y + (approxCardSize.Height / 2), 0, -1, traceLen, colorMatchCardBorder, out Point hitBottom);
                bool bHasHit2 = ImageUtils.TraceLine(bitmap, scanAnchors[Idx].X - (approxCardSize.Width / 2), scanAnchors[Idx].Y, 1, 0, traceLen, colorMatchCardBorder, out Point hitLeft);
                bool bHasHit3 = ImageUtils.TraceLine(bitmap, scanAnchors[Idx].X + (approxCardSize.Width / 2), scanAnchors[Idx].Y, -1, 0, traceLen, colorMatchCardBorder, out Point hitRight);

                if (bHasHit0 && bHasHit1 && bHasHit2 && bHasHit3)
                {
                    scanRect[Idx] = new Rectangle(hitLeft.X - scanAnchors[Idx].X, hitTop.Y - scanAnchors[Idx].Y, hitRight.X - hitLeft.X, hitBottom.Y - hitTop.Y);
                    //if (debugMode) { debugShapes.Add(new Rectangle(hitLeft.X, hitTop.Y, scanRect[Idx].Width, scanRect[Idx].Height)); }
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
            if (debugMode) { Logger.WriteLine("FindBuleCardCoords: " + stopwatch.ElapsedMilliseconds + "ms"); }
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
            List<Point> spanV = ImageUtils.TraceSpansV(bitmap, noTokenRect, colorMatchRuleText, 5);
            foreach (Point span in spanV)
            {
                Point boundsH = ImageUtils.TraceBoundsH(bitmap, new Rectangle(rulesRect.Left, span.X, rulesRect.Width, span.Y), colorMatchRuleText, span.Y * 2);
                if (boundsH.Y > 5)
                {
                    Rectangle ruleTextBox = new Rectangle(boundsH.X, span.X, boundsH.Y, span.Y);
                    FastBitmapHash ruleHashData = ImageUtils.CalculateImageHash(bitmap, ruleTextBox, rulesRect, colorMatchRuleText, 64, 8);

                    TriadGameModifier bestModOb = (TriadGameModifier)ImageUtils.FindMatchingHash(ruleHashData, EImageHashType.Rule, out int bestDistance, debugMode);
                    if (bestModOb == null)
                    {
                        Bitmap cachedScreenshot = screenAnalyzer.screenReader.cachedScreenshot;

                        bool isValid =
                            (ruleHashData.ContextBounds.Top >= 0) &&
                            (ruleHashData.ContextBounds.Left >= 0) &&
                            (ruleHashData.ContextBounds.Bottom < cachedScreenshot.Height) &&
                            (ruleHashData.ContextBounds.Right < cachedScreenshot.Width);

                        if (isValid)
                        {
                            ImageUtils.ConditionalAddUnknownHash(
                                ImageUtils.CreateUnknownImageHash(ruleHashData, cachedScreenshot, EImageHashType.Rule), screenAnalyzer.unknownHashes);

                            screenAnalyzer.OnUnknownHashAdded();
                        }
                        else
                        {
                            Logger.WriteLine("ParseRules ERROR: out of bounds! screenshot:" + cachedScreenshot.Width + "x" + cachedScreenshot.Height + ", hashContext:" + ruleHashData.ContextBounds + ", ruleBox:" + rulesRect + ", fastBitmap:" + bitmap.Width + "x" + bitmap.Height);
                        }
                    }
                    else
                    {
                        ruleHashData.GuideOb = bestModOb;
                        screenAnalyzer.currentHashDetections.Add(ruleHashData, bestDistance);

                        rules.Add(bestModOb);
                    }

                    debugHashes.Add(ruleHashData);
                }
            }

            stopwatch.Stop();
            if (debugMode) { Logger.WriteLine("ParseRules: " + stopwatch.ElapsedMilliseconds + "ms"); }
        }

        private TriadCard ParseCard(FastBitmapHSV bitmap, Rectangle cardRect, List<ImagePatternDigit> digitList, string debugName, out bool bIsGreyedOut)
        {
            bIsGreyedOut = false;

            bool bIsBoardCard = debugName.StartsWith("board");
            if (bIsBoardCard)
            {
                // check average color of frame vs mid part of card as a backup plan
                Rectangle topFrameRect = new Rectangle(cardRect.Left + (cardRect.Width * 25 / 100), cardRect.Top,
                    cardRect.Width * 50 / 100, 5);

                Rectangle cardMidRect = new Rectangle(cardRect.Left + (cardRect.Width * 25 / 100), cardRect.Top + (cardRect.Height * 60 / 100),
                    cardRect.Width * 50 / 100, cardRect.Height * 40 / 100);

                FastPixelHSV avgColorFrame = ImageUtils.GetAverageColor(bitmap, topFrameRect);
                FastPixelHSV avgColorMid = ImageUtils.GetAverageColor(bitmap, cardMidRect);
                int avgColorDiff = Math.Abs(avgColorFrame.GetHue() - avgColorMid.GetHue()) + Math.Abs(avgColorFrame.GetSaturation() - avgColorMid.GetSaturation());

                // low color diff: empty / hidden
                bool bIsColorDiffLow = avgColorDiff < 15;
                if (bIsColorDiffLow)
                {
                    if (debugMode) { Logger.WriteLine("ParseCard(" + debugName + "): empty, diff:" + avgColorDiff + " (" + avgColorFrame + " vs " + avgColorMid + ")"); }
                    return null;
                }
            }
            else
            {
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
                    if (debugMode) { Logger.WriteLine("ParseCard(" + debugName + "): empty, counter:" + borderMatchCount); }
                    return null;
                }
            }

            // check if card is hidden based on approx numberbox location
            Rectangle numberBox = new Rectangle(cardRect.Left + (cardRect.Width * 27 / 100),    // moved slightly off center to right
                cardRect.Top + (cardRect.Height * 74 / 100),
                cardRect.Width * 50 / 100,
                cardRect.Height * 18 / 100);

            float borderPct = ImageUtils.CountFillPct(bitmap, numberBox, colorMatchCardBorder);
            if (borderPct > 0.75f)
            {
                if (debugMode) { Logger.WriteLine("ParseCard(" + debugName + "): hidden, fill:" + (int)(100 * borderPct) + "%"); }
                return TriadCardDB.Get().hiddenCard;
            }

            ImageUtils.FindColorRange(bitmap, numberBox, out int minMono, out int maxMono);
            FastPixelMatch colorMatchNumAdjusted = (maxMono < 220) ? new FastPixelMatchMono((byte)(maxMono * 85 / 100), 255) : colorMatchCardNumber;
            bIsGreyedOut = (maxMono < 220);

            // find numbers
            if (debugMode) { debugShapes.Add(new Rectangle(numberBox.Left - 1, numberBox.Top - 1, numberBox.Width + 2, numberBox.Height + 2)); }

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
                    CardNumbers[Idx] = ImageUtils.FindPatternMatch(bitmap, cardScanAnchors[Idx], colorMatchNumAdjusted, digitList, out detectionState.sideImage[Idx]);
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
                    CardNumbers[Idx] = ImageUtils.FindPatternMatchScaled(bitmap, cardScanAnchors[Idx], sadDigitSize, colorMatchNumAdjusted, digitList, out detectionState.sideImage[Idx]);
                    debugShapes.Add(new Rectangle(cardScanAnchors[Idx], sadDigitSize));
                }
            };

            TriadCard foundCard = TriadCardDB.Get().Find(CardNumbers[0], CardNumbers[1], CardNumbers[2], CardNumbers[3]);
            if (debugMode)
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
                FastBitmapHash cardHashData = ImageUtils.CalculateImageHash(bitmap, cardHashBox, cardRect, colorMatchAll, 32, 8);
                cardHashData.GuideOb = foundCard;

                TriadCard bestCardOb = (TriadCard)ImageUtils.FindMatchingHash(cardHashData, EImageHashType.Card, out int bestDistance, debugMode);
                if (bestCardOb == null)
                {
                    ImageUtils.ConditionalAddUnknownHash(
                        ImageUtils.CreateUnknownImageHash(cardHashData, screenAnalyzer.screenReader.cachedScreenshot, EImageHashType.Card), screenAnalyzer.unknownHashes);

                    screenAnalyzer.OnUnknownHashAdded();
                }
                else
                {
                    foundCard = bestCardOb;

                    cardHashData.GuideOb = bestCardOb;
                    screenAnalyzer.currentHashDetections.Add(cardHashData, bestDistance);
                }

                debugHashes.Add(cardHashData);
            }

            detectionState.name = debugName;
            detectionState.card = foundCard;
            detectionState.failedMatching = (foundCard == null);
            detectionState.sideNumber = new int[4] { CardNumbers[0], CardNumbers[1], CardNumbers[2], CardNumbers[3] };
            cachedCardState.Add(detectionState);

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
            if (debugMode) { debugShapes.Add(testBounds); }

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
            if (debugMode) { Logger.WriteLine(">> owner: " + owner + " (" + counterRed + " red vs " + counterBlue + " blue)"); }
            return owner;
        }

        private bool ParseTimer(FastBitmapHSV bitmap, Rectangle timerRect)
        {
            int scanY = (timerRect.Top + timerRect.Bottom) / 2;
            bool bHasActiveTimer = ImageUtils.TraceLine(bitmap, timerRect.Left, scanY, 1, 0, timerRect.Width, colorMatchTimerActive, out Point dummyHit);
            if (debugMode) { Logger.WriteLine("ParseTimer: " + (bHasActiveTimer ? "blue turn" : "waiting...")); }
            return bHasActiveTimer;
        }

        #endregion

        #region Validation

        private enum EVerifyCardState
        {
            None,
            Hidden,
            Locked,
            Visible,
            PlacedRed,
            PlacedBlue,
        }

        private class VerifyCard
        {
            public EVerifyCardState state;
            public int[] sides;
            public int mod;

            public VerifyCard()
            {
                state = EVerifyCardState.None;
                sides = new int[4] { 0, 0, 0, 0 };
                mod = 0;
            }

            public override string ToString()
            {
                return string.Format("{0} [{1},{2},{3},{4}] {5}",
                    state, sides[0], sides[1], sides[2], sides[3],
                    mod == 0 ? "" : (mod > 0 ? ("+" + mod) : mod.ToString()));
            }
        }

        private class VerifyConfig
        {
            public string[] rules;
            public VerifyCard[] deckBlue;
            public VerifyCard[] deckRed;
            public VerifyCard[] board;

            public VerifyConfig()
            {
                rules = null;
                deckBlue = new VerifyCard[5];
                deckRed = new VerifyCard[5];
                board = new VerifyCard[9];
            }

            public override string ToString()
            {
                return (rules == null) ? "No rules" : string.Join(", ", rules);
            }
        }

        private static VerifyCard ParseConfigCardData(JsonParser.ObjectValue cardOb)
        {
            VerifyCard cardData = new VerifyCard();

            string stateDesc = cardOb["state"] as JsonParser.StringValue;
            if (stateDesc == "empty") cardData.state = EVerifyCardState.None;
            else if (stateDesc == "hidden") cardData.state = EVerifyCardState.Hidden;
            else if (stateDesc == "locked") cardData.state = EVerifyCardState.Locked;
            else if (stateDesc == "visible") cardData.state = EVerifyCardState.Visible;
            else if (stateDesc == "red") cardData.state = EVerifyCardState.PlacedRed;
            else if (stateDesc == "blue") cardData.state = EVerifyCardState.PlacedBlue;

            if (cardOb.entries.ContainsKey("sides"))
            {
                JsonParser.ArrayValue sidesArr = cardOb.entries["sides"] as JsonParser.ArrayValue;
                for (int idx = 0; idx < 4; idx++)
                {
                    cardData.sides[idx] = sidesArr.entries[idx] as JsonParser.IntValue;
                }
            }

            if (cardOb.entries.ContainsKey("mod"))
            {
                cardData.mod = cardOb["mod"] as JsonParser.IntValue;
            }

            return cardData;
        }

        private static VerifyConfig LoadValidationConfig(string configPath)
        {
            VerifyConfig configData = new VerifyConfig();
            string configText = File.ReadAllText(configPath);

            JsonParser.ObjectValue rootOb = JsonParser.ParseJson(configText);

            JsonParser.ArrayValue ruleArr = rootOb.entries["rules"] as JsonParser.ArrayValue;
            configData.rules = new string[ruleArr.entries.Count];
            for (int idx = 0; idx < ruleArr.entries.Count; idx++)
            {
                configData.rules[idx] = ruleArr.entries[idx] as JsonParser.StringValue;
            }

            JsonParser.ArrayValue deckRedArr = rootOb.entries["deckRed"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < deckRedArr.entries.Count; idx++)
            {
                configData.deckRed[idx] = ParseConfigCardData(deckRedArr.entries[idx] as JsonParser.ObjectValue);
            }

            JsonParser.ArrayValue deckBlueArr = rootOb.entries["deckBlue"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < deckBlueArr.entries.Count; idx++)
            {
                configData.deckBlue[idx] = ParseConfigCardData(deckBlueArr.entries[idx] as JsonParser.ObjectValue);
            }

            JsonParser.ArrayValue boardArr = rootOb.entries["board"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < boardArr.entries.Count; idx++)
            {
                configData.board[idx] = ParseConfigCardData(boardArr.entries[idx] as JsonParser.ObjectValue);
            }

            return configData;
        }

        private static Dictionary<string, TriadGameModifier> mapValidationRules;

        public override void ValidateScan(string configPath, ScreenAnalyzer.EMode mode)
        {
            string testName = Path.GetFileNameWithoutExtension(configPath);

            VerifyConfig configData = LoadValidationConfig(configPath);
            if (configData == null || cachedGameState == null)
            {
                string exceptionMsg = string.Format("Test {0} failed! Scan results:{1}, config path: {2}", testName, cachedGameState, configPath);
                throw new Exception(exceptionMsg);
            }

            // fixup missing rules
            int numAddedRules = 0;
            for (int ruleIdx = 0; ruleIdx < configData.rules.Length; ruleIdx++)
            {
                int readRuleIdx = ruleIdx - numAddedRules;
                bool hasMatchingRule = false;

                if (readRuleIdx < cachedGameState.mods.Count)
                {
                    string readRuleName = cachedGameState.mods[readRuleIdx].GetName();
                    hasMatchingRule = readRuleName == configData.rules[ruleIdx];
                }

                if (!hasMatchingRule)
                {
                    if (mapValidationRules == null)
                    {
                        mapValidationRules = new Dictionary<string, TriadGameModifier>();
                        foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
                        {
                            if (type.IsSubclassOf(typeof(TriadGameModifier)))
                            {
                                TriadGameModifier modInstance = (TriadGameModifier)Activator.CreateInstance(type);
                                mapValidationRules.Add(modInstance.GetName(), modInstance);
                            }
                        }
                    }

                    if (screenAnalyzer.unknownHashes.Count > 0)
                    {
                        ImageHashData hashData = new ImageHashData(mapValidationRules[configData.rules[ruleIdx]], screenAnalyzer.unknownHashes[0].hashData.Hash, screenAnalyzer.unknownHashes[0].hashData.Type);
                        PlayerSettingsDB.Get().AddKnownHash(hashData);
                        PlayerSettingsDB.Get().Save();

                        screenAnalyzer.PopUnknownHash();
                        numAddedRules++;
                    }
                    else
                    {
                        string exceptionMsg = string.Format("Test {0} failed! Can't match rules!", testName);
                        throw new Exception(exceptionMsg);
                    }
                }
            }

            // verify decks
            TriadCard lockedBlueCard = cachedGameState.forcedBlueCard;
            for (int idx = 0; idx < 5; idx++)
            {
                TriadCard blueCard = cachedGameState.blueDeck[idx];
                EVerifyCardState blueState =
                    (blueCard == null) ? EVerifyCardState.None :
                    blueCard.Name == failedMatchCard.Name ? EVerifyCardState.Visible :
                    !blueCard.IsValid() ? EVerifyCardState.Hidden :
                    (lockedBlueCard == null || lockedBlueCard == blueCard) ? EVerifyCardState.Visible :
                    EVerifyCardState.Locked;

                if (blueState != configData.deckBlue[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Blue[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        blueState,
                        configData.deckBlue[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (blueCard != null &&
                    (blueCard.Sides[0] != configData.deckBlue[idx].sides[0] ||
                    blueCard.Sides[1] != configData.deckBlue[idx].sides[1] ||
                    blueCard.Sides[2] != configData.deckBlue[idx].sides[2] ||
                    blueCard.Sides[3] != configData.deckBlue[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Blue[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        blueCard.Sides[0], blueCard.Sides[1], blueCard.Sides[2], blueCard.Sides[3],
                        configData.deckBlue[idx].sides[0], configData.deckBlue[idx].sides[1], configData.deckBlue[idx].sides[2], configData.deckBlue[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }

                TriadCard redCard = cachedGameState.redDeck[idx];
                EVerifyCardState redState =
                    (redCard == null) ? EVerifyCardState.None :
                    redCard.Name == failedMatchCard.Name ? EVerifyCardState.Visible :
                    !redCard.IsValid() ? EVerifyCardState.Hidden :
                    EVerifyCardState.Visible;

                if (configData.deckRed[idx].state == EVerifyCardState.Locked) { configData.deckRed[idx].state = EVerifyCardState.Visible; }
                if (redState != configData.deckRed[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Red[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        redState,
                        configData.deckRed[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (redCard != null &&
                    (redCard.Sides[0] != configData.deckRed[idx].sides[0] ||
                    redCard.Sides[1] != configData.deckRed[idx].sides[1] ||
                    redCard.Sides[2] != configData.deckRed[idx].sides[2] ||
                    redCard.Sides[3] != configData.deckRed[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Red[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        redCard.Sides[0], redCard.Sides[1], redCard.Sides[2], redCard.Sides[3],
                        configData.deckRed[idx].sides[0], configData.deckRed[idx].sides[1], configData.deckRed[idx].sides[2], configData.deckRed[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }
            }

            // verify board
            for (int idx = 0; idx < 9; idx++)
            {
                TriadCard testCard = cachedGameState.board[idx];
                EVerifyCardState cardState =
                    (testCard == null) ? EVerifyCardState.None :
                    cachedGameState.boardOwner[idx] == ETriadCardOwner.Red ? EVerifyCardState.PlacedRed :
                    cachedGameState.boardOwner[idx] == ETriadCardOwner.Blue ? EVerifyCardState.PlacedBlue :
                    EVerifyCardState.None;

                if (cardState != configData.board[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Board[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        cardState,
                        configData.board[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (testCard != null &&
                    (testCard.Sides[0] != configData.board[idx].sides[0] ||
                    testCard.Sides[1] != configData.board[idx].sides[1] ||
                    testCard.Sides[2] != configData.board[idx].sides[2] ||
                    testCard.Sides[3] != configData.board[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Board[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        testCard.Sides[0], testCard.Sides[1], testCard.Sides[2], testCard.Sides[3],
                        configData.board[idx].sides[0], configData.board[idx].sides[1], configData.board[idx].sides[2], configData.board[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }
            }
        }

        #endregion
    }
}
