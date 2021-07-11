using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for OverlayWindowTransparent.xaml
    /// </summary>
    public partial class OverlayWindowTransparent : Window
    {
        public FrameworkElement panelCapture;

        public OverlayWindowTransparent()
        {
            InitializeComponent();
        }

        public void SetOverlayActive(bool wantsActive)
        {
            Visibility = wantsActive ? Visibility.Visible : Visibility.Hidden;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this).Handle;
            var wndStyleEx = (OverlayWindowInteractive.GetWindowLong(helper, OverlayWindowInteractive.GWL_EX_STYLE) | OverlayWindowInteractive.WS_EX_TOOLWINDOW | OverlayWindowInteractive.WS_EX_TRANSPARENT) & ~OverlayWindowInteractive.WS_EX_APPWINDOW;
            OverlayWindowInteractive.SetWindowLong(helper, OverlayWindowInteractive.GWL_EX_STYLE, wndStyleEx);
        }

        public void SetDetailsCanvasPos(double x = -1, double y = -1)
        {
            var clampedX = (x < 0) ? Canvas.GetLeft(panelCapture) : x;
            var clampedY = (y < 0) ? Canvas.GetTop(panelCapture) : y;

            if (panelDetails.ActualWidth > 0)
            {
                Canvas.SetLeft(panelDetails, clampedX - panelDetails.ActualWidth - 10);
                Canvas.SetTop(panelDetails, clampedY);
            }

            if (panelCapture.ActualWidth > 0)
            {
                Canvas.SetLeft(panelBoard, clampedX + panelCapture.ActualWidth + 10);
                Canvas.SetTop(panelBoard, clampedY);
            }
        }

        private void panelDetails_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetDetailsCanvasPos();
        }

        private void panelDetails_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                SetDetailsCanvasPos();
            }
        }

        private void panelBoard_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                SetDetailsCanvasPos();
            }
        }
    }
}
