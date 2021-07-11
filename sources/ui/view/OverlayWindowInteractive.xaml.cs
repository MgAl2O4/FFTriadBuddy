using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for OverlayWindowInteractive.xaml
    /// </summary>
    public partial class OverlayWindowInteractive : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        public const int GWL_EX_STYLE = -20;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TRANSPARENT = 0x00000020;

        public event Action<double, double> OnPanelMoved;
        private Point moveStartPt;

        public OverlayWindowInteractive()
        {
            InitializeComponent();

            if (PlayerSettingsDB.Get().useXInput)
            {
                XInputStub.StartPolling();
            }
        }

        public void SetOverlayActive(bool wantsActive)
        {
            Visibility = wantsActive ? Visibility.Visible : Visibility.Hidden;
            SetXInputEnble(wantsActive);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this).Handle;
            var wndStyleEx = (OverlayWindowInteractive.GetWindowLong(helper, OverlayWindowInteractive.GWL_EX_STYLE) | OverlayWindowInteractive.WS_EX_TOOLWINDOW) & ~OverlayWindowInteractive.WS_EX_APPWINDOW;
            OverlayWindowInteractive.SetWindowLong(helper, OverlayWindowInteractive.GWL_EX_STYLE, wndStyleEx);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            XInputStub.StopPolling();
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.OverrideCursor != null)
            {
                if (Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    Mouse.OverrideCursor = null;
                }
                else
                {
                    var border = sender as Border;
                    var movePt = e.GetPosition(this);

                    var newX = Canvas.GetLeft(border) + movePt.X - moveStartPt.X;
                    var newY = Canvas.GetTop(border) + movePt.Y - moveStartPt.Y;
                    SetPanelCanvasPos(newX, newY);

                    moveStartPt = movePt;
                }
            }
        }

        public void SetPanelCanvasPos(double x, double y)
        {
            var clampedX = Math.Max(0, Math.Min(canvas.ActualWidth - panelCapture.ActualWidth, x));
            var clampedY = Math.Max(0, Math.Min(canvas.ActualHeight - panelCapture.ActualHeight, y));

            Canvas.SetLeft(panelCapture, clampedX);
            Canvas.SetTop(panelCapture, clampedY);

            OnPanelMoved?.Invoke(clampedX, clampedY);
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.OverrideCursor == Cursors.SizeAll)
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeAll;
            moveStartPt = e.GetPosition(this);
        }

        public void SetXInputEnble(bool enable)
        {
            if (enable)
            {
                XInputStub.OnEventMotionTrigger += XInputEventMotion;
            }
            else
            {
                XInputStub.OnEventMotionTrigger -= XInputEventMotion;
            }
        }

        private void XInputEventMotion()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                buttonCapture.Command.Execute(null);
            });
        }
    }
}
