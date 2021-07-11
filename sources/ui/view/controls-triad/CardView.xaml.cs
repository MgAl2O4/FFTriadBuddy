using System.Windows;
using System.Windows.Controls;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for CardView.xaml
    /// </summary>
    public partial class CardView : UserControl
    {
        public static readonly DependencyProperty IsBlinkingOnHighlightProperty = DependencyProperty.Register("IsBlinkingOnHighlight", typeof(bool), typeof(DeckView));

        public bool IsBlinkingOnHighlight
        {
            get { return (bool)GetValue(IsBlinkingOnHighlightProperty); }
            set { SetValue(IsBlinkingOnHighlightProperty, value); }
        }

        public CardView()
        {
            InitializeComponent();
        }
    }
}
