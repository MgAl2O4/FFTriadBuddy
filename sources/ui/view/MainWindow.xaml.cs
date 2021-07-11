using System.Windows;
using System.Windows.Input;

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
    }
}
