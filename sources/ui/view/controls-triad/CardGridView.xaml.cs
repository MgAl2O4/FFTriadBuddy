using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for CardGridView.xaml
    /// </summary>
    public partial class CardGridView : UserControl
    {
        public CardGridView()
        {
            InitializeComponent();
        }

        private void CardView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var controlOb = sender as FrameworkElement;
            var cardVM = controlOb.DataContext as CardViewModel;
            var gridVM = DataContext as CardCollectionViewModel;

            if (gridVM.CommandSelect.CanExecute(cardVM))
            {
                gridVM.CommandSelect.Execute(cardVM);
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // maintain aspect ratio
            // TODO: read those from cardGrid item?
            int numRows = 6;
            int numColumns = 5;

            Width = numColumns * Math.Max(0, ActualHeight - gridName.ActualHeight) / numRows;
        }
    }
}
