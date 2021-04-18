using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace FFTriadBuddy
{
    public class ScannerBase
    {
        public class GameStateBase
        {
        }

        protected ScreenAnalyzer screenAnalyzer;
        protected bool debugMode;

        public List<Rectangle> debugShapes;
        public List<FastBitmapHash> debugHashes;

        public GameStateBase cachedGameStateBase;

        public void Initialize(ScreenAnalyzer parent)
        {
            screenAnalyzer = parent;
            debugShapes = new List<Rectangle>();
            debugHashes = new List<FastBitmapHash>();
            debugMode = false;
        }

        public virtual void InvalidateCache()
        {
        }

        public virtual bool HasValidCache(FastBitmapHSV bitmap, int scannerFlags)
        {
            return false;
        }

        public virtual bool DoWork(FastBitmapHSV bitmap, int scannerFlags, Stopwatch perfTimer, bool debugMode)
        {
            this.debugMode = debugMode;
            debugShapes.Clear();
            debugHashes.Clear();

            return false;
        }

        public virtual void AppendDebugShapes(List<Rectangle> shapes)
        {
            shapes.AddRange(debugShapes);
        }

        public virtual void ValidateScan(string configPath, ScreenAnalyzer.EMode mode)
        {
            throw new Exception("Scanner doesn't support tests!");
        }
    }
}
