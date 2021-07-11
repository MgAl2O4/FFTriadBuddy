using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageCards.xaml
    /// </summary>
    public partial class PageCards : UserControl
    {
        public PageCards()
        {
            InitializeComponent();
        }

        private void TabItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var findCtxMenu = listCards.FindResource("searchCtx") as ContextMenu;
                if (findCtxMenu != null)
                {
                    const int PlacementPadding = 10;
                    findCtxMenu.Placement = PlacementMode.RelativePoint;
                    findCtxMenu.PlacementTarget = listCards;
                    findCtxMenu.VerticalOffset = PlacementPadding;
                    findCtxMenu.IsOpen = true;

                    findCtxMenu.HorizontalOffset = listCards.ActualWidth - findCtxMenu.ActualWidth - PlacementPadding;

                    var textBox = ViewUtils.FindVisualChildRecursive(findCtxMenu, x => x is TextBox) as TextBox;
                    textBox?.Focus();
                    textBox?.SelectAll();
                }
            }
        }

        private void searchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var pageVM = DataContext as PageCardsViewModel;

            if (pageVM.CommandSearchCard.CanExecute(textBox.Text))
            {
                pageVM.CommandSearchCard.Execute(textBox.Text);
            }
        }

        private void listCards_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listView = e.Source as ListView;
            var pageVM = DataContext as PageCardsViewModel;
            var paramOb = (listView.ContextMenu.Tag ?? listView.SelectedItem) as CardModelProxy;

            if (pageVM.CommandBuildContextActions.CanExecute(paramOb))
            {
                pageVM.CommandBuildContextActions.Execute(paramOb);
            }
        }
    }
}
