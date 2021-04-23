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

        public enum ECardLocation
        {
            BlueDeck,
            RedDeck,
            Board,
        }

        public enum ECardState
        {
            None,
            Hidden,
            Locked,
            Visible,
            PlacedRed,
            PlacedBlue,
        }

        public class CardState : IComparable<CardState>
        {
            public struct SideInfo
            {
                public float matchPct;
                public int matchNum;
                public bool hasOverride;
                public Rectangle scanBox;
                public float[] hashValues;
            }

            public TriadCard card;
            public string name;
            public int[] sideNumber;
            public bool failedMatching;

            public ECardState state;
            public ECardLocation location;
            public int locationContext;
            public Rectangle scanBox;
            public Rectangle bounds;
            public Bitmap sourceImage;
            public ImageHashData cardImageHash;
            public SideInfo[] sideInfo;

            public int CompareTo(CardState other)
            {
                return (failedMatching != other.failedMatching) ? (failedMatching ? -1 : 1) :
                    (location != other.location) ? location.CompareTo(other.location) :
                    locationContext.CompareTo(other.locationContext);
            }

            public override string ToString()
            {
                return string.Format("{0}: {1} [{2}]", name, card, state);
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

        public GameState cachedGameState;
        public List<CardState> cachedCardState = new List<CardState>();
        public EScanError cachedScanError = EScanError.NoErrors;

        private static Size digitHashSize = new Size(10, 10);

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
        private MLClassifierTriadDigit classifierTriadDigit;

        public ScannerTriad()
        {
            classifierTriadDigit = new MLClassifierTriadDigit();
            classifierTriadDigit.InitializeModel();
        }

        public override void InvalidateCache()
        {
            base.InvalidateCache();
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
        public static Size GetDigitHashSize() { return digitHashSize; }

        public override void AppendDebugShapes(List<Rectangle> shapes, List<ImageUtils.HashPreview> hashes)
        {
            base.AppendDebugShapes(shapes, hashes);
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
                    screenAnalyzer.ClearKnownHashes();
                    cachedGameState = new GameState();
                    cachedGameStateBase = cachedGameState;

                    ParseRules(bitmap, cachedRuleBox, cachedGameState.mods);

                    bool bCanContinue = (screenAnalyzer.GetCurrentState() != ScreenAnalyzer.EState.UnknownHash) || debugMode;
                    if (bCanContinue)
                    {
                        cachedScanError = EScanError.NoErrors;
                        cachedCardState.Clear();

                        {
                            perfTimer.Restart();

                            bool hasLockedBlueCards = false;
                            for (int Idx = 0; Idx < 5; Idx++)
                            {
                                CardState blueCardState = ParseCard(bitmap, cachedBlueCards[Idx], "blue" + Idx, ECardLocation.BlueDeck);
                                blueCardState.locationContext = Idx;
                                cachedCardState.Add(blueCardState);

                                CardState redCardState = ParseCard(bitmap, cachedRedCards[Idx], "red" + Idx, ECardLocation.RedDeck);
                                redCardState.location = ECardLocation.RedDeck;
                                redCardState.locationContext = Idx;
                                cachedCardState.Add(redCardState);

                                cachedGameState.blueDeck[Idx] = blueCardState.card;
                                cachedGameState.redDeck[Idx] = redCardState.card;
                                if (blueCardState.state == ECardState.Locked)
                                {
                                    hasLockedBlueCards = true;
                                }
                            }

                            cachedGameState.forcedBlueCard = null;
                            if (hasLockedBlueCards)
                            {
                                for (int Idx = 0; Idx < 5; Idx++)
                                {
                                    if (cachedCardState[Idx].card != null &&
                                        cachedCardState[Idx].state == ECardState.Visible &&
                                        cachedCardState[Idx].location == ECardLocation.BlueDeck)
                                    {
                                        cachedGameState.forcedBlueCard = cachedCardState[Idx].card;
                                        break;
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
                                CardState boardCardState = ParseCard(bitmap, cachedBoardCards[Idx], "board" + Idx, ECardLocation.Board);
                                boardCardState.locationContext = Idx;

                                cachedGameState.board[Idx] = boardCardState.card;
                                if (boardCardState.state == ECardState.Visible || boardCardState.card != null)
                                {
                                    int gridCellLeft = cachedGridBox.Left + ((Idx % 3) * cachedGridBox.Width / 3);
                                    cachedGameState.boardOwner[Idx] = ParseCardOwner(bitmap, cachedBoardCards[Idx], gridCellLeft, "board" + Idx);

                                    boardCardState.state = (cachedGameState.boardOwner[Idx] == ETriadCardOwner.Blue) ? ECardState.PlacedBlue : ECardState.PlacedRed;
                                }

                                cachedCardState.Add(boardCardState);
                            }

                            perfTimer.Stop();
                            if (debugMode) { Logger.WriteLine("Parse board: " + perfTimer.ElapsedMilliseconds + "ms"); }
                        }

                        foreach (CardState cardState in cachedCardState)
                        {
                            if (cardState.failedMatching)
                            {
                                cachedScanError = EScanError.FailedCardMatching;
                                break;
                            }
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
                    const int hashWidth = 64;
                    const int hashHeight = 8;

                    Rectangle ruleTextBox = new Rectangle(boundsH.X, span.X, boundsH.Y, span.Y);
                    float[] values = ImageUtils.ExtractImageFeaturesScaled(bitmap, ruleTextBox, hashWidth, hashHeight, testPx => colorMatchRuleText.IsMatching(testPx) ? 1.0f : 0.0f);

                    ImageHashData rulePattern = new ImageHashData() { type = EImageHashType.Rule, previewBounds = ruleTextBox, previewContextBounds = rulesRect };
                    rulePattern.CalculateHash(values);

                    ImageHashData foundPattern = ImageHashDB.Get().FindBestMatch(rulePattern, 100, out int matchDistance);
                    if (foundPattern != null)
                    {
                        rulePattern.ownerOb = foundPattern.ownerOb;
                        rulePattern.matchDistance = matchDistance;
                        rulePattern.isKnown = true;

                        rules.Add((TriadGameModifier)foundPattern.ownerOb);
                    }

                    screenAnalyzer.AddImageHash(rulePattern);

                    Rectangle previewBounds = new Rectangle(ruleTextBox.Right + 10, ruleTextBox.Top, hashWidth, hashHeight);
                    debugHashes.Add(new ImageUtils.HashPreview() { hashValues = values, bounds = previewBounds });
                }
            }

            stopwatch.Stop();
            if (debugMode) { Logger.WriteLine("ParseRules: " + stopwatch.ElapsedMilliseconds + "ms"); }
        }

        private bool FindExactCardBottom(FastBitmapHSV bitmap, Rectangle cardRect, string debugName, ECardLocation cardLocation, out int exactBottomY)
        {
            // look for edge of border, sample 4 points along card width for avg
            int[] scanX = new int[4]{
                    cardRect.Left + (cardRect.Width * 20 / 100),
                    cardRect.Left + (cardRect.Width * 40 / 100),
                    cardRect.Left + (cardRect.Width * 60 / 100),
                    cardRect.Left + (cardRect.Width * 80 / 100)};

            int scanEndY = cardRect.Bottom - 10;
            int scanStartY = cardRect.Bottom + 5;

            bool verboseDebug = screenAnalyzer.debugScannerContext == debugName;
            if (cardLocation == ECardLocation.Board)
            {
                // find bottom of grid cell
                if (ImageUtils.TraceLine(bitmap, cardRect.Left - 10, scanEndY, 0, 1, 20, colorMatchGridBorder, out Point hitPos, verboseDebug))
                {
                    scanStartY = hitPos.Y - 1;
                }
            }

            int prevAvgMono = 0;
            int prevAvgHue = 0;

            for (int scanY = scanStartY; scanY > scanEndY; scanY--)
            {
                int avgMono = 0;
                int avgHue = 0;
                for (int idxX = 0; idxX < scanX.Length; idxX++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(scanX[idxX], scanY);
                    avgMono += testPx.GetMonochrome();
                    avgHue += testPx.GetHue();
                }
                avgMono /= scanX.Length;
                avgHue /= scanX.Length;

                if (prevAvgMono > 0)
                {
                    int diffMono = avgMono - prevAvgMono;
                    int absDiffHue = Math.Abs(avgHue - prevAvgHue);

                    // mono: no abs, look for going darker (low - high = negative)
                    // hue: abs, jump in hue will be good indicator, esp on red/blue decks
                    bool foundEdge = (diffMono < -30 && avgMono < 100) || (absDiffHue > 120);

                    if (verboseDebug) { Logger.WriteLine("FindExactCardBottom[{0}] scan:{1} = [H:{2},M:{3}] vs prev[H:{4},M:{5}] => {6}", debugName, scanY, avgHue, avgMono, prevAvgHue, prevAvgMono, foundEdge ? " EDGE!" : "nope"); }
                    if (foundEdge)
                    {
                        exactBottomY = scanY;
                        return true;
                    }
                }

                prevAvgMono = avgMono;
                prevAvgHue = avgHue;
            }

            exactBottomY = cardRect.Bottom;
            return false;
        }

        private bool FindExactCardTop(FastBitmapHSV bitmap, Rectangle cardRect, string debugName, ECardLocation cardLocation, out int exactTopY)
        {
            // look for edge of border, sample 4 points along card width for avg
            int[] scanX = new int[4]{
                    cardRect.Left + (cardRect.Width * 20 / 100),
                    cardRect.Left + (cardRect.Width * 40 / 100),
                    cardRect.Left + (cardRect.Width * 60 / 100),
                    cardRect.Left + (cardRect.Width * 80 / 100)};

            int scanEndY = cardRect.Top + 10;
            int scanStartY = cardRect.Top - 5;

            bool verboseDebug = screenAnalyzer.debugScannerContext == debugName;
            if (cardLocation == ECardLocation.Board)
            {
                // find top of grid cell
                if (ImageUtils.TraceLine(bitmap, cardRect.Left - 10, scanEndY, 0, -1, 20, colorMatchGridBorder, out Point hitPos, verboseDebug))
                {
                    scanStartY = hitPos.Y + 1;
                }
            }

            int prevAvgMono = 0;
            int prevAvgHue = 0;

            for (int scanY = scanStartY; scanY < scanEndY; scanY++)
            {
                int avgMono = 0;
                int avgHue = 0;
                for (int idxX = 0; idxX < scanX.Length; idxX++)
                {
                    FastPixelHSV testPx = bitmap.GetPixel(scanX[idxX], scanY);
                    avgMono += testPx.GetMonochrome();
                    avgHue += testPx.GetHue();
                }
                avgMono /= scanX.Length;
                avgHue /= scanX.Length;

                if (prevAvgMono > 0)
                {
                    int diffMono = avgMono - prevAvgMono;
                    int absDiffHue = Math.Abs(avgHue - prevAvgHue);

                    // mono: no abs, look for going darker (low - high = negative)
                    // hue: abs, jump in hue will be good indicator, esp on red/blue decks
                    bool foundEdge = (diffMono < -30 && avgMono < 100) || (absDiffHue > 120);

                    if (verboseDebug) { Logger.WriteLine("FindExactCardTop[{0}] scan:{1} = [H:{2},M:{3}] vs prev[H:{4},M:{5}] => {6}", debugName, scanY, avgHue, avgMono, prevAvgHue, prevAvgMono, foundEdge ? " EDGE!" : "nope"); }
                    if (foundEdge)
                    {
                        exactTopY = scanY;
                        return true;
                    }
                }

                prevAvgMono = avgMono;
                prevAvgHue = avgHue;
            }

            exactTopY = cardRect.Top;
            return false;
        }

        private int FindExactCardMidX(FastBitmapHSV bitmap, Rectangle cardRect, string debugName, int exactBottomY)
        {
            int scanHeight = Math.Min(5, cardRect.Height * 3 / 100);
            int midX = cardRect.X + (cardRect.Width / 2);
            int startX = midX - (cardRect.Width / 8);
            int endX = midX + (cardRect.Width / 8);

            bool verboseDebug = screenAnalyzer.debugScannerContext == debugName;
            int bestV = -1;
            int maxEmpty = scanHeight / 2;
            for (int scanX = startX; scanX < endX; scanX++)
            {
                int monoV = 0;
                int numEmpty = 0;
                bool canCountEmpty = true;

                for (int scanY = 0; scanY < scanHeight; scanY++)
                {
                    int testMono = bitmap.GetPixel(scanX, exactBottomY - scanY - 1).GetMonochrome();
                    bool isEmpty = testMono > 100;
                    numEmpty += (isEmpty && canCountEmpty) ? 1 : 0;
                    canCountEmpty = canCountEmpty && isEmpty;

                    monoV += testMono * ((scanY + 1) / scanHeight);
                }

                bool isBetter = (monoV < bestV || bestV < 0) && (numEmpty < maxEmpty);
                if (verboseDebug)
                {
                    Logger.WriteLine("FindExactCardMidX[{0}] scan:[{1},{2}..{3}], numEmpty:{4} = {5}{6}", debugName, 
                        scanX, exactBottomY - 1, exactBottomY - scanHeight, 
                        numEmpty,
                        monoV, isBetter ? " mid?" : ""); 
                }
                if (isBetter)
                {
                    bestV = monoV;
                    midX = scanX;
                }
            }

            return midX;
        }

        private float[] ExtractCardNumberPattern(FastBitmapHSV bitmap, Rectangle scanBoxNumber)
        {
            return ImageUtils.ExtractImageFeaturesScaled(bitmap, scanBoxNumber, digitHashSize.Width, digitHashSize.Height, ImageUtils.GetPixelFeaturesM2V2);
        }

        private CardState ParseCard(FastBitmapHSV bitmap, Rectangle cardRect, string debugName, ECardLocation cardLocation)
        {
            CardState cardState = new CardState();
            cardState.name = debugName;
            cardState.location = cardLocation;
            cardState.bounds = cardRect;
            cardState.sourceImage = screenAnalyzer.screenReader.cachedScreenshot;

            int exactCardTop = cardRect.Top;
            bool hasExactBottomY = FindExactCardBottom(bitmap, cardRect, debugName, cardLocation, out int exactCardBottom);
            bool hasExactTopY = hasExactBottomY ? FindExactCardTop(bitmap, cardRect, debugName, cardLocation, out exactCardTop) : false;
            if (!hasExactBottomY || !hasExactTopY)
            {
                if (debugMode) { Logger.WriteLine("ParseCard({0}): empty", debugName); }
                return cardState;
            }
           
            int exactCardMidX = FindExactCardMidX(bitmap, cardRect, debugName, exactCardBottom);

            // check if card is hidden based on approx numberbox location
            int exactCardHeight = exactCardBottom - exactCardTop;
            int numberBoxW = cardRect.Width * 50 / 100;
            int numberBoxH = exactCardHeight * 18 / 100;
            Rectangle numberBox = new Rectangle(exactCardMidX - (numberBoxW / 2), exactCardBottom - numberBoxH - (exactCardHeight * 9 / 100), numberBoxW, numberBoxH);

            float borderPct = ImageUtils.CountFillPct(bitmap, numberBox, colorMatchCardBorder);
            if (borderPct > 0.75f)
            {
                if (debugMode) { Logger.WriteLine("ParseCard({0}): hidden, fill:{1:P0}", debugName, borderPct); }
                cardState.state = ECardState.Hidden;

                return cardState;
            }

            ImageUtils.FindColorRange(bitmap, numberBox, out int minMono, out int maxMono);
            FastPixelMatch colorMatchNumAdjusted = (maxMono < 200) ? new FastPixelMatchMono((byte)(maxMono * 85 / 100), 255) : colorMatchCardNumber;
            cardState.state = (maxMono < 200) ? ECardState.Locked : ECardState.Visible;
            cardState.scanBox = numberBox;

            // find numbers
            if (debugMode) { debugShapes.Add(new Rectangle(numberBox.Left - 1, numberBox.Top - 1, numberBox.Width + 2, numberBox.Height + 2)); }

            int numberBoxMidX = (numberBox.Left + numberBox.Right) / 2;
            int numberBoxMidY = (numberBox.Top + numberBox.Bottom) / 2;
            int digitHeight = numberBox.Height * 50 / 100;
            int digitWidth = digitHeight * digitHashSize.Width / digitHashSize.Height;

            cardState.sideNumber = new int[4];
            cardState.sideInfo = new CardState.SideInfo[4];
            cardState.sideInfo[0].scanBox = new Rectangle(numberBoxMidX - (digitWidth / 2), numberBox.Top, digitHeight, digitHeight);
            cardState.sideInfo[1].scanBox = new Rectangle(numberBoxMidX + (digitWidth * 70 / 100), numberBoxMidY - (digitHeight / 2), digitHeight, digitHeight);
            cardState.sideInfo[2].scanBox = new Rectangle(numberBoxMidX - (digitWidth / 2), numberBox.Bottom - digitHeight, digitHeight, digitHeight);
            cardState.sideInfo[3].scanBox = new Rectangle(numberBoxMidX - (digitWidth * 150 / 100), numberBoxMidY - (digitHeight / 2), digitHeight, digitHeight);

            for (int idx = 0; idx < 4; idx++)
            {
                float[] values = ExtractCardNumberPattern(bitmap, cardState.sideInfo[idx].scanBox);
                ImageUtils.NormalizeImageFeatures(values);

                ImageHashData digitPattern = new ImageHashData() { type = EImageHashType.CardNumber, previewBounds = cardState.sideInfo[idx].scanBox, previewContextBounds = numberBox, isKnown = true };
                digitPattern.CalculateHash(values);

                cardState.sideInfo[idx].matchNum = classifierTriadDigit.Calculate(values, out cardState.sideInfo[idx].matchPct);

                // allow overwrites in case there is a user defined value, must be exact match
                ImageHashData overridePattern = ImageHashDB.Get().FindExactMatch(digitPattern);
                if (overridePattern != null)
                {
                    cardState.sideNumber[idx] = (int)overridePattern.ownerOb;
                    cardState.sideInfo[idx].hasOverride = true;
                }
                else
                {
                    cardState.sideNumber[idx] = cardState.sideInfo[idx].matchNum;
                    digitPattern.isAuto = true;
                }

                cardState.sideInfo[idx].hashValues = values;
                screenAnalyzer.AddImageHash(digitPattern);

                Rectangle previewBounds = new Rectangle(cardState.sideInfo[idx].scanBox.X + numberBox.Width, cardState.sideInfo[idx].scanBox.Y, digitHashSize.Width, digitHashSize.Height);
                debugHashes.Add(new ImageUtils.HashPreview() { hashValues = values, bounds = previewBounds });
                debugShapes.Add(cardState.sideInfo[idx].scanBox);
            }

            TriadCard foundCard = TriadCardDB.Get().Find(cardState.sideNumber[0], cardState.sideNumber[1], cardState.sideNumber[2], cardState.sideNumber[3]);
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

                Logger.WriteLine("ParseCard({0}): {1}-{2}-{3}-{4} => {5}", debugName,
                    cardState.sideNumber[0], cardState.sideNumber[1], cardState.sideNumber[2], cardState.sideNumber[3],
                    descFoundCards);
            }

            // more than one card found
            if (foundCard != null && foundCard.SameNumberId >= 0)
            {
                Rectangle cardHashBox = new Rectangle(cardRect.Left + (cardRect.Width * 15 / 100), cardRect.Top + (cardRect.Height * 70 / 100), cardRect.Width * 70 / 100, cardRect.Height * 25 / 100);
                float[] values = ImageUtils.ExtractImageFeaturesScaled(bitmap, cardHashBox, 32, 8, ImageUtils.GetPixelFeaturesMono);

                ImageHashData cardPattern = new ImageHashData() { type = EImageHashType.CardImage, previewBounds = cardHashBox, previewContextBounds = cardRect };
                cardPattern.CalculateHash(values);
                cardPattern.ownerOb = foundCard;

                ImageHashData foundPattern = ImageHashDB.Get().FindBestMatch(cardPattern, 100, out int matchDistance);
                if (foundPattern != null)
                {
                    cardPattern.ownerOb = foundPattern.ownerOb;
                    cardPattern.matchDistance = matchDistance;
                    cardPattern.isKnown = true;
                }

                cardState.cardImageHash = cardPattern;
                screenAnalyzer.AddImageHash(cardPattern);

                debugHashes.Add(new ImageUtils.HashPreview() { hashValues = values, bounds = cardHashBox });
            }

            cardState.name = debugName;
            cardState.card = foundCard;
            cardState.failedMatching = (foundCard == null);
            return cardState;
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

        private class VerifyCard
        {
            public ECardState state;
            public int[] sides;
            public int mod;

            public VerifyCard()
            {
                state = ECardState.None;
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
            if (stateDesc == "empty") cardData.state = ECardState.None;
            else if (stateDesc == "hidden") cardData.state = ECardState.Hidden;
            else if (stateDesc == "locked") cardData.state = ECardState.Locked;
            else if (stateDesc == "visible") cardData.state = ECardState.Visible;
            else if (stateDesc == "red") cardData.state = ECardState.PlacedRed;
            else if (stateDesc == "blue") cardData.state = ECardState.PlacedBlue;

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

        public override void ValidateScan(string configPath, ScreenAnalyzer.EMode mode, MLDataExporter dataExporter)
        {
            string testName = Path.GetFileNameWithoutExtension(configPath);

            VerifyConfig configData = LoadValidationConfig(configPath);
            if (configData == null || cachedGameState == null)
            {
                string exceptionMsg = string.Format("Test {0} failed! Scan results:{1}, config path: {2}", testName, cachedGameState, configPath);
                throw new Exception(exceptionMsg);
            }

            List<ImageHashData> unknownRulePatterns = new List<ImageHashData>();
            for (int idx = 0; idx < screenAnalyzer.unknownHashes.Count; idx++)
            {
                ImageHashData testHash = screenAnalyzer.unknownHashes[idx];
                switch (testHash.type)
                {
                    case EImageHashType.Rule: unknownRulePatterns.Add(testHash); break;
                    default: break;
                }
            }

            List<ImageHashData> matchedRulePatterns = new List<ImageHashData>();
            for (int idx = 0; idx < screenAnalyzer.currentHashMatches.Count; idx++)
            {
                ImageHashData testHash = screenAnalyzer.currentHashMatches[idx];
                switch (testHash.type)
                {
                    case EImageHashType.Rule: matchedRulePatterns.Add(testHash); break;
                    default: break;
                }
            }

            int numRulesScanned = cachedGameState.mods.Count + unknownRulePatterns.Count;
            if (numRulesScanned != configData.rules.Length)
            {
                string exceptionMsg = string.Format("Test {0} failed! Rules known:{1} + unknown:{2}, total:{3}, expected:{4}",
                    testName,
                    cachedGameState.mods.Count, unknownRulePatterns.Count, numRulesScanned,
                    configData.rules.Length);
                throw new Exception(exceptionMsg);
            }

            if (dataExporter != null)
            {
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

                        ImageHashData rulePattern;
                        if (numAddedRules >= unknownRulePatterns.Count)
                        {
                            rulePattern = matchedRulePatterns[ruleIdx];
                            // don't increment numAddedRules, it's for unknown hashes only
                        }
                        else
                        {
                            rulePattern = unknownRulePatterns[numAddedRules];
                            numAddedRules++;
                        }

                        rulePattern.ownerOb = mapValidationRules[configData.rules[ruleIdx]];
                        PlayerSettingsDB.Get().AddKnownHash(rulePattern);
                        PlayerSettingsDB.Get().Save();

                        Logger.WriteLine("Exported rule pattern:{0}", rulePattern.ownerOb);
                    }
                }
            }
            else if (unknownRulePatterns.Count > 0)
            {
                string exceptionMsg = string.Format("Test {0} failed! Rules not recognized, unknown:{1}", testName, unknownRulePatterns.Count);
                throw new Exception(exceptionMsg);
            }

            Action<VerifyCard, ECardLocation, int> ValidateCard = (verifyCard, cardLocation, locationCtx) =>
            {
                CardState detectedCard = null;
                foreach (var testState in cachedCardState)
                {
                    if (testState.location == cardLocation && testState.locationContext == locationCtx)
                    {
                        detectedCard = testState;
                        break;
                    }
                }

                string debugCardName = ((cardLocation == ECardLocation.BlueDeck) ? "blue" : (cardLocation == ECardLocation.RedDeck) ? "red" : "board") + locationCtx;
                if (detectedCard == null)
                {
                    screenAnalyzer.debugScannerContext = debugCardName;
                    string exceptionMsg = string.Format("Test {0} failed! {1}[{2}] is missing card, expected:{3}",
                        testName, cardLocation, locationCtx, verifyCard.state);

                    throw new Exception(exceptionMsg);
                }

                if (detectedCard.state != verifyCard.state)
                {
                    screenAnalyzer.debugScannerContext = debugCardName;
                    string exceptionMsg = string.Format("Test {0} failed! {1}[{2}] got:{3}, expected:{4}",
                        testName, cardLocation, locationCtx, detectedCard.state, verifyCard.state);

                    throw new Exception(exceptionMsg);
                }

                if (verifyCard.state == ECardState.Locked ||
                    verifyCard.state == ECardState.Visible ||
                    verifyCard.state == ECardState.PlacedBlue ||
                    verifyCard.state == ECardState.PlacedRed)
                {
                    if (dataExporter != null)
                    {
                        // generate additional sample images by offseting source bounds a few pixels
                        const int offsetExt = 1;
                        int numPatterns = 0;

                        for (int offsetX = -offsetExt; offsetX <= offsetExt; offsetX++)
                        {
                            for (int offsetY = -offsetExt; offsetY <= offsetExt; offsetY++)
                            {
                                for (int sideIdx = 0; sideIdx < 4; sideIdx++)
                                {
                                    Rectangle offsetBounds = detectedCard.sideInfo[sideIdx].scanBox;
                                    offsetBounds.Offset(offsetX, offsetY);

                                    float[] exportValues = ExtractCardNumberPattern(screenAnalyzer.cachedFastBitmap, offsetBounds);
                                    dataExporter.ExportValues(exportValues, verifyCard.sides[sideIdx]);
                                    numPatterns++;
                                }
                            }
                        }

                        Logger.WriteLine("Exported ML entries:{0}", numPatterns);
                    }
                    else
                    {
                        if (detectedCard.sideNumber[0] != verifyCard.sides[0] ||
                            detectedCard.sideNumber[1] != verifyCard.sides[1] ||
                            detectedCard.sideNumber[2] != verifyCard.sides[2] ||
                            detectedCard.sideNumber[3] != verifyCard.sides[3])
                        {
                            screenAnalyzer.debugScannerContext = debugCardName;
                            string exceptionMsg = string.Format("Test {0} failed! {1}[{2}] got:[{3},{4},{5},{6}], expected:[{7},{8},{9},{10}]",
                                testName, cardLocation, locationCtx,
                                detectedCard.sideNumber[0], detectedCard.sideNumber[1], detectedCard.sideNumber[2], detectedCard.sideNumber[3],
                                verifyCard.sides[0], verifyCard.sides[1], verifyCard.sides[2], verifyCard.sides[3]);
                            //throw new Exception(exceptionMsg);
                            Logger.WriteLine(exceptionMsg);
                        }
                    }
                }
            };

            for (int idx = 0; idx < 5; idx++)
            {
                ValidateCard(configData.deckBlue[idx], ECardLocation.BlueDeck, idx);
                ValidateCard(configData.deckRed[idx], ECardLocation.RedDeck, idx);
            }

            for (int idx = 0; idx < 9; idx++)
            {
                ValidateCard(configData.board[idx], ECardLocation.Board, idx);
            }
        }

        #endregion
    }
}
