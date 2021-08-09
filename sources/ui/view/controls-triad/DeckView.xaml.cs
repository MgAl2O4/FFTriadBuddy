using MgAl2O4.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for DeckView.xaml
    /// </summary>
    public partial class DeckView : UserControl
    {
        public static readonly DependencyProperty CanReorderCardsProperty = DependencyProperty.Register("CanReorderCards", typeof(bool), typeof(DeckView));
        public static readonly DependencyProperty CanPickCardsProperty = DependencyProperty.Register("CanPickCards", typeof(bool), typeof(DeckView));

        public bool CanReorderCards
        {
            get { return (bool)GetValue(CanReorderCardsProperty); }
            set { SetValue(CanReorderCardsProperty, value); }
        }
        public bool CanPickCards
        {
            get { return (bool)GetValue(CanPickCardsProperty); }
            set { SetValue(CanPickCardsProperty, value); }
        }

        private static bool hasMetadataOverride = false;

        public DeckView()
        {
            InitializeComponent();

            // force default size for instances
            if (!hasMetadataOverride)
            {
                hasMetadataOverride = true;
                WidthProperty.OverrideMetadata(typeof(DeckView), new FrameworkPropertyMetadata((double)280));
                HeightProperty.OverrideMetadata(typeof(DeckView), new FrameworkPropertyMetadata((double)50));
            }
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var controlOb = sender as Control;
            var cardVM = controlOb.DataContext as CardViewModel;
            var deckVM = DataContext as DeckViewModel;

            if (IsEnabled && deckVM.CommandSelect.CanExecute(cardVM))
            {
                deckVM.CommandSelect.Execute(cardVM);
            }
        }

        private void Card_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsEnabled && CanPickCards)
            {
                var controlOb = sender as Control;
                var cardVM = controlOb.DataContext as CardViewModel;

                if (cardVM.CardDragMode == ECardDragMode.None)
                {
                    var controlCtxMenu = ContextMenuService.GetContextMenu(controlOb);
                    if (controlCtxMenu != null)
                    {
                        controlCtxMenu.Placement = PlacementMode.Top;
                        controlCtxMenu.PlacementTarget = controlOb;
                        controlCtxMenu.IsOpen = true;
                    }

                    CardDragDropExtension.CardDragReset();
                }
            }
        }

        private void SearchableComboBox_SelectionEffectivelyChanged(object sender, object obj)
        {
            var controlOb = sender as Control;
            var deckVM = DataContext as DeckViewModel;

            var commandParam = new DeckViewModel.CardPickerParams() { cardVM = controlOb.DataContext as CardViewModel, cardModel = obj as CardModelProxy };
            if (IsEnabled && deckVM.CommandPickCard.CanExecute(commandParam))
            {
                deckVM.CommandPickCard.Execute(commandParam);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // validate, there are reports of default selection not matching actual card

            var ctxMenu = sender as ContextMenu;
            var comboBox = ViewUtils.FindVisualChildRecursive(ctxMenu, x => x is SearchableComboBox) as SearchableComboBox;
            if (comboBox != null)
            {
                var selectedEntry = comboBox.SelectedItem as CardModelProxy;
                var expectedEntry = (ctxMenu.DataContext as CardViewModel)?.CardModel;

                if (selectedEntry != expectedEntry)
                {
                    comboBox.SelectedItem = expectedEntry;

                    if (Logger.IsSuperVerbose())
                    {
                        var selectedEntry2 = comboBox.SelectedItem as CardModelProxy;

                        Logger.WriteLine("Card context selection failed! got:{0}, expected:{1}, after forcing:{2}",
                            selectedEntry?.DescDeckPicker ?? "--",
                            expectedEntry?.DescDeckPicker ?? "--",
                            selectedEntry2?.DescDeckPicker ?? "--");
                    }
                }
            }
        }
    }
}
