using MgAl2O4.Utils;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public class ScreenReader
    {
        public enum EState
        {
            NoErrors,
            MissingGameProcess,
            MissingGameWindow,
            MissingFile,
        }

        private Process cachedProcess;
        private Screen cachedScreen;
        private Rectangle cachedGameWindow;
        private float cachedScreenScaling;

        public Bitmap cachedScreenshot;
        public EState currentState;

        public bool TakeScreenshot(Rectangle optClipBounds)
        {
            bool result = false;

            HandleRef windowHandle = FindGameWindow();
            if (windowHandle.Handle.ToInt64() != 0)
            {
                result = (optClipBounds.Width > 0) ? CaptureWindowPartial(windowHandle, optClipBounds) : CaptureWindow(windowHandle);
            }

            return result;
        }

        public bool LoadScreenshot(string path, Rectangle optClipBounds)
        {
            if (File.Exists(path))
            {
                //if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }

                cachedScreenshot = new Bitmap(path);
                if (cachedScreenshot != null)
                {
                    if (optClipBounds.Width > 0)
                    {
                        Bitmap croppedBitmap = cachedScreenshot.Clone(optClipBounds, cachedScreenshot.PixelFormat);
                        cachedScreenshot.Dispose();
                        cachedScreenshot = croppedBitmap;
                    }

                    cachedGameWindow = new Rectangle(0, 0, cachedScreenshot.Width, cachedScreenshot.Height);
                    currentState = EState.NoErrors;
                    return true;
                }
            }

            currentState = EState.MissingFile;
            return false;
        }

        public void InitializeScreenData()
        {
            currentState = EState.NoErrors;

            HandleRef windowHandle = FindGameWindow();
            if (windowHandle.Handle.ToInt64() != 0)
            {
                cachedGameWindow = GetGameWindowBounds(windowHandle);
            }
            else
            {
                cachedGameWindow = Rectangle.Empty;
            }
        }

        public Rectangle ConvertGameToScreen(Rectangle gameBounds)
        {
            if (cachedGameWindow.Width <= 0 || cachedScreen == null) { return Rectangle.Empty; }

            return new Rectangle(
                cachedScreen.Bounds.X + (int)((gameBounds.X + cachedGameWindow.X - cachedScreen.Bounds.X) * cachedScreenScaling),
                cachedScreen.Bounds.Y + (int)((gameBounds.Y + cachedGameWindow.Y - cachedScreen.Bounds.Y) * cachedScreenScaling),
                (int)(gameBounds.Width * cachedScreenScaling),
                (int)(gameBounds.Height * cachedScreenScaling)
                );
        }


        public Rectangle GetCachedGameWindow()
        {
            return cachedGameWindow;
        }

        private HandleRef FindGameWindow()
        {
            HandleRef WindowHandle = new HandleRef();
            bool useVerboseLogs = Logger.IsSuperVerbose();
            string wndNamePrefix = loc.strings.Game_WindowNamePrefix;
            string wndNamePrefixDefault = "FINAL FANTASY";

            bool hasCached = false;
            if (cachedProcess != null)
            {
                string cachedWndTitle = cachedProcess.MainWindowTitle;
                hasCached = cachedWndTitle.StartsWith(wndNamePrefix) || cachedWndTitle.StartsWith(wndNamePrefixDefault);
            }

            if (!hasCached)
            {
                Process[] processes = Process.GetProcessesByName(loc.strings.Game_ProcessName_DX11);
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName(loc.strings.Game_ProcessName_DX9);
                    if (processes.Length == 0)
                    {
                        if (loc.strings.Game_ProcessName_DX11 != "ffxiv_dx11")
                        {
                            processes = Process.GetProcessesByName("ffxiv_dx11");
                            if (processes.Length == 0)
                            {
                                if (loc.strings.Game_ProcessName_DX9 != "ffxiv")
                                {
                                    processes = Process.GetProcessesByName("ffxiv");
                                }
                            }
                        }
                    }
                }

                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: process list to check: " + processes.Length); }

                cachedProcess = null;
                foreach (Process p in processes)
                {
                    string procWndTitle = p.MainWindowTitle;
                    bool hasMatchingTitle = procWndTitle.StartsWith(wndNamePrefix) || procWndTitle.StartsWith(wndNamePrefixDefault);
                    if (useVerboseLogs)
                    {
                        Logger.WriteLine(">> pid:{0}, name:{1}, window:'{2}', hwnd:0x{3:x} => {4}",
                            p.Id, p.ProcessName, p.MainWindowTitle, p.MainWindowHandle.ToInt64(),
                            (hasMatchingTitle ? "match!" : "nope"));

                        try { Logger.WriteLine("   path:'{0}'", p.MainModule.FileName); }
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
                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: 0x{0:x}", cachedProcess.MainWindowHandle.ToInt64()); }
                WindowHandle = new HandleRef(this, cachedProcess.MainWindowHandle);
            }
            else
            {
                if (useVerboseLogs) { Logger.WriteLine("FindGameWindow: can't find window!"); }
                currentState = EState.MissingGameProcess;
            }

            return WindowHandle;
        }

        private Rectangle GetGameWindowBoundsRaw(HandleRef windowHandle)
        {
            Rectangle result = new Rectangle(0, 0, 0, 0);

            bool bHasWindow = windowHandle.Handle.ToInt64() != 0;
            if (bHasWindow)
            {
                if (GetWindowRect(windowHandle, out RECT windowRectApi))
                {
                    result = new Rectangle(windowRectApi.Left, windowRectApi.Top, windowRectApi.Right - windowRectApi.Left, windowRectApi.Bottom - windowRectApi.Top);
                    if (Logger.IsSuperVerbose())
                    {
                        Logger.WriteLine("GetGameWindowBoundsRaw: handle:0x{0}, bounds:{1}, api:{2}", windowHandle.Handle.ToInt64(), result, windowRectApi);
                    }
                }
            }

            return result;
        }

        private Rectangle GetGameWindowBounds(HandleRef windowHandle)
        {
            Rectangle result = GetGameWindowBoundsRaw(windowHandle);
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

                if (Logger.IsSuperVerbose())
                {
                    Logger.WriteLine("GetGameWindowBounds, caching screen data: bounds:{0}, pelsWidth:{1} => scale:{2}", cachedScreen.Bounds, dm.dmPelsWidth, cachedScreenScaling);
                }
            }

            if (cachedScreenScaling != 1.0f)
            {
                result.X = cachedScreen.Bounds.X + (int)((result.X - cachedScreen.Bounds.X) / cachedScreenScaling);
                result.Y = cachedScreen.Bounds.Y + (int)((result.Y - cachedScreen.Bounds.Y) / cachedScreenScaling);
                result.Width = (int)(result.Width / cachedScreenScaling);
                result.Height = (int)(result.Height / cachedScreenScaling);
            }

            return result;
        }

        private bool CaptureWindow(HandleRef windowHandle)
        {
            bool useVerboseLogs = Logger.IsSuperVerbose();
            bool result = false;

            Rectangle bounds = GetGameWindowBounds(windowHandle);
            if (bounds.Width > 0)
            {
                cachedGameWindow = bounds;
                if (useVerboseLogs) { Logger.WriteLine("TakeScreenshot: bounds " + cachedGameWindow); }

                //if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                cachedScreenshot = new Bitmap(cachedGameWindow.Width, cachedGameWindow.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(cachedScreenshot))
                {
                    // can't use PrintWindow API, returns black screen
                    // copy entire screen - will capture all windows on top of game too
                    g.CopyFromScreen(cachedGameWindow.Location, Point.Empty, cachedGameWindow.Size);
                    if (useVerboseLogs) { Logger.WriteLine(">> copied from screen"); }
                    result = true;
                }
            }
            else
            {
                currentState = EState.MissingGameWindow;
            }

            return result;
        }

        private bool CaptureWindowPartial(HandleRef windowHandle, Rectangle innerBounds)
        {
            bool useVerboseLogs = Logger.IsSuperVerbose();
            bool result = false;

            Rectangle bounds = GetGameWindowBounds(windowHandle);
            if (bounds.Width > 0 && innerBounds.Width > 0)
            {
                if (useVerboseLogs) { Logger.WriteLine("TakeScreenshotPartial: bounds " + bounds); }

                //if (cachedScreenshot != null) { cachedScreenshot.Dispose(); }
                cachedScreenshot = new Bitmap(innerBounds.Width, innerBounds.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(cachedScreenshot))
                {
                    // copy entire screen - will capture all windows on top of game too
                    Point copyPt = new Point(bounds.Left + innerBounds.Left, bounds.Top + innerBounds.Top);
                    g.CopyFromScreen(copyPt, Point.Empty, innerBounds.Size);
                }

                result = true;
            }
            else
            {
                currentState = EState.MissingGameWindow;
            }

            return result;
        }

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
    }
}
