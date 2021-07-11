using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageNpcs.xaml
    /// </summary>
    public partial class PageNpcs : UserControl
    {
        public PageNpcs()
        {
            InitializeComponent();
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var findCtxMenu = listNpcs.FindResource("searchCtx") as ContextMenu;
                if (findCtxMenu != null)
                {
                    const int PlacementPadding = 10;
                    findCtxMenu.Placement = PlacementMode.RelativePoint;
                    findCtxMenu.PlacementTarget = listNpcs;
                    findCtxMenu.VerticalOffset = PlacementPadding;
                    findCtxMenu.IsOpen = true;

                    findCtxMenu.HorizontalOffset = listNpcs.ActualWidth - findCtxMenu.ActualWidth - PlacementPadding;

                    var textBox = ViewUtils.FindVisualChildRecursive(findCtxMenu, x => x is TextBox) as TextBox;
                    textBox?.Focus();
                    textBox?.SelectAll();
                }
            }
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var pageVM = DataContext as PageNpcsViewModel;

            if (pageVM.CommandSearchNpc.CanExecute(textBox.Text))
            {
                pageVM.CommandSearchNpc.Execute(textBox.Text);
            }
        }

        private void listNpcs_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listView = e.Source as ListView;
            var pageVM = DataContext as PageNpcsViewModel;
            var paramOb = (listView.ContextMenu.Tag ?? listView.SelectedItem) as NpcModelProxy;

            if (pageVM.CommandBuildContextActions.CanExecute(paramOb))
            {
                pageVM.CommandBuildContextActions.Execute(paramOb);
            }
        }
    }
}
