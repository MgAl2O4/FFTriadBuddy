using System.Windows;

namespace FFTriadBuddy.UI
{
    public class OverlayWindowService : IOverlayWindowService
    {
        private OverlayWindowInteractive overlayWindowI;
        private OverlayWindowTransparent overlayWindowT;

        public void SetOverlayActive(IOverlayWindowViewModel viewModel, bool wantsActive)
        {
            if (overlayWindowI == null)
            {
                overlayWindowI = new OverlayWindowInteractive() { Owner = App.Current.MainWindow, FontSize = App.Current.MainWindow.FontSize };
            }

            if (overlayWindowT == null)
            {
                overlayWindowT = new OverlayWindowTransparent() { Owner = App.Current.MainWindow, FontSize = App.Current.MainWindow.FontSize };
                overlayWindowT.panelCapture = overlayWindowI.panelCapture;
                overlayWindowI.OnPanelMoved += (x, y) => overlayWindowT.SetDetailsCanvasPos(x, y);
            }

            overlayWindowI.DataContext = viewModel;
            overlayWindowT.DataContext = viewModel;

            overlayWindowI.SetOverlayActive(wantsActive);
            overlayWindowT.SetOverlayActive(wantsActive);
        }

        public bool IsCursorInside(System.Drawing.Rectangle screenBounds)
        {
            if (overlayWindowI != null)
            {
                var screenPos = System.Windows.Forms.Cursor.Position;
                bool isInside = screenBounds.Contains(screenPos.X, screenPos.Y);

                if (isInside)
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveOverlayToScreen(System.Drawing.Rectangle screenBounds)
        {
            if (overlayWindowI != null)
            {
                overlayWindowI.Left = screenBounds.X;
                overlayWindowI.Top = screenBounds.Y;
                overlayWindowI.Width = screenBounds.Width;
                overlayWindowI.Height = screenBounds.Height;
            }

            if (overlayWindowT != null)
            {
                overlayWindowT.Left = screenBounds.X;
                overlayWindowT.Top = screenBounds.Y;
                overlayWindowT.Width = screenBounds.Width;
                overlayWindowT.Height = screenBounds.Height;
            }
        }

        public void OnProcessingGameWindow(System.Drawing.Rectangle gameWindowBounds, out bool invalidatedPosition)
        {
            invalidatedPosition = false;
            if (!gameWindowBounds.IsEmpty && (overlayWindowI != null))
            {
                var screenBounds = System.Windows.Forms.Screen.GetBounds(gameWindowBounds);
                var centerPt = new Point(overlayWindowI.Left + (overlayWindowI.ActualWidth * 0.5), overlayWindowI.Top + (overlayWindowI.ActualHeight * 0.5));
                if (!screenBounds.Contains((int)centerPt.X, (int)centerPt.Y))
                {
                    MoveOverlayToScreen(screenBounds);
                    invalidatedPosition = true;
                }
            }
        }

        public System.Drawing.Rectangle GetScreenBounds(System.Drawing.Rectangle gameWindowBounds)
        {
            OnProcessingGameWindow(gameWindowBounds, out bool _);
            if (gameWindowBounds.IsEmpty)
            {
                var mainWindow = App.Current.MainWindow;
                gameWindowBounds = new System.Drawing.Rectangle((int)mainWindow.Left, (int)mainWindow.Top, (int)mainWindow.ActualWidth, (int)mainWindow.ActualHeight);
            }

            var screenBounds = System.Windows.Forms.Screen.GetBounds(gameWindowBounds);
            MoveOverlayToScreen(screenBounds);

            return screenBounds;
        }

        public static void Initialize()
        {
            ViewModelServices.OverlayWindow = new OverlayWindowService();
        }
    }
}
