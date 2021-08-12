using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                var mainVM = DataContext as MainWindowViewModel;
                if (mainVM.CommandDebugScreenshot.CanExecute(null))
                {
                    mainVM.CommandDebugScreenshot.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var settingsDB = PlayerSettingsDB.Get();
            settingsDB.lastWidth = (float)Width;
            settingsDB.lastHeight = (float)Height;
        }
    }

    public class AppWindowService : IAppWindowService
    {
        public void SetAlwaysOnTop(bool value)
        {
            var mainWindow = App.Current.MainWindow;
            mainWindow.Topmost = value;
        }

        public void SetFontSize(float value)
        {
            var mainWindow = App.Current.MainWindow;
            mainWindow.FontSize = value;

            foreach (Window window in mainWindow.OwnedWindows)
            {
                window.FontSize = mainWindow.FontSize;
            }
        }

        public void SetSoftwareRendering(bool value)
        {
            RenderOptions.ProcessRenderMode = value ? RenderMode.SoftwareOnly : RenderMode.Default;
        }

        public static void Initialize()
        {
            ViewModelServices.AppWindow = new AppWindowService();
        }
    }
}
