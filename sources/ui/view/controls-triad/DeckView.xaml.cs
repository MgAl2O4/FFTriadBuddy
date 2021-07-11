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
    }
}
