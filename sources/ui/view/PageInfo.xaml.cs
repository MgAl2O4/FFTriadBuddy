using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageInfo.xaml
    /// </summary>
    public partial class PageInfo : UserControl
    {
        public PageInfo()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
