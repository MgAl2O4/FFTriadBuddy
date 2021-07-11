using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PlayerDeckPreview.xaml
    /// </summary>
    public partial class PlayerDeckPreview : UserControl
    {
        public PlayerDeckPreview()
        {
            InitializeComponent();
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var controlOb = sender as FrameworkElement;
            var cardVM = controlOb.DataContext as CardViewModel;
            var deckVM = DataContext as DeckViewModel;

            if (deckVM.CommandSelect.CanExecute(cardVM))
            {
                deckVM.CommandSelect.Execute(cardVM);
            }
        }
    }
}
