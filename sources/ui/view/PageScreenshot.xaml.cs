using System.Windows.Controls;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageScreenshot.xaml
    /// </summary>
    public partial class PageScreenshot : UserControl
    {
        public PageScreenshot()
        {
            InitializeComponent();
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var param = checkToggleOverlay.CommandParameter;
            if (checkToggleOverlay.Command.CanExecute(param))
            {
                checkToggleOverlay.Command.Execute(param);
            }
        }

        private void ListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listView = e.Source as ListView;
            var pageVM = DataContext as PageScreenshotViewModel;

            if (pageVM.CommandBuildContextActions.CanExecute(listView.SelectedItem))
            {
                pageVM.CommandBuildContextActions.Execute(listView.SelectedItem);
            }
        }
    }
}
