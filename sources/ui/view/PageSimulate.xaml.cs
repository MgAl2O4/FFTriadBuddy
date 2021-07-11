using System;
using System.Windows;
using System.Windows.Controls;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageSimulate.xaml
    /// </summary>
    public partial class PageSimulate : UserControl
    {
        public PageSimulate()
        {
            InitializeComponent();
        }

        private void rectPlayerDeckSlot_LayoutUpdated(object sender, EventArgs e)
        {
            // position playerDeck to fit inside rectPlayerDeckSlot, anchored with upper right corner
            // ...and keeping 3x2 aspect ratio

            var cellSize = Math.Min(rectPlayerDeckSlot.ActualWidth / 3, rectPlayerDeckSlot.ActualHeight / 2);
            playerDeck.Width = cellSize * 3;
            playerDeck.Height = cellSize * 2;

            var anchorPoint = rectPlayerDeckSlot.TranslatePoint(new Point(rectPlayerDeckSlot.ActualWidth, 0), this);
            playerDeck.Margin = new Thickness(anchorPoint.X - playerDeck.Width, anchorPoint.Y, 0, 0);
        }
    }
}
