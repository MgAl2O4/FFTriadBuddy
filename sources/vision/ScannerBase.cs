using MgAl2O4.Utils;
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
        public List<ImageUtils.HashPreview> debugHashes;
        public GameStateBase cachedGameStateBase;

        public void Initialize(ScreenAnalyzer parent)
        {
            screenAnalyzer = parent;
            debugShapes = new List<Rectangle>();
            debugHashes = new List<ImageUtils.HashPreview>();
            debugMode = false;
        }

        public virtual void InvalidateCache()
        {
            cachedGameStateBase = null;
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

        public virtual void AppendDebugShapes(List<Rectangle> shapes, List<ImageUtils.HashPreview> hashes)
        {
            shapes.AddRange(debugShapes);
            hashes.AddRange(debugHashes);
        }

        public virtual void ValidateScan(string configPath, ScreenAnalyzer.EMode mode, MLDataExporter dataExporter)
        {
            throw new Exception("Scanner doesn't support tests!");
        }
    }
}
