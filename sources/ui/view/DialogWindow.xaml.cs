using System;
using System.Windows;
using System.Windows.Controls;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for DialogWindow.xaml
    /// </summary>
    public partial class DialogWindow : Window
    {
        public DialogWindow()
        {
            InitializeComponent();

            Loaded += DialogWindow_Loaded;
        }

        private void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var viewControl = ViewUtils.FindVisualChildRecursive(this, x => x is UserControl) as UserControl;
            if (viewControl != null)
            {
                var controlRoot = viewControl.Content as FrameworkElement;
                var presenter = viewControl.TemplatedParent as FrameworkElement;

                if (presenter != null && controlRoot != null && controlRoot.ActualWidth > 0 && controlRoot.ActualHeight > 0)
                {
                    Size bounds = new Size(controlRoot.ActualWidth, controlRoot.ActualHeight);
                    presenter.Height = bounds.Height;
                    presenter.Width = bounds.Width;

                    SizeToContent = SizeToContent.WidthAndHeight;
                    MinHeight = ActualHeight;
                    MinWidth = ActualWidth;

                    SizeToContent = SizeToContent.Manual;
                    presenter.Height = double.NaN;
                    presenter.Width = double.NaN;

                    // reposition to center after setting size
                    Left = Owner.Left + Math.Max(0, (Owner.Width - Width) * 0.5f);
                    Top = Owner.Top + Math.Max(0, (Owner.Height - Height) * 0.5f);
                }
            }
        }
    }

    public class DialogWindowService : IDialogWindowService
    {
        public bool? ShowDialog(IDialogWindowViewModel viewModel)
        {
            var window = new DialogWindow
            {
                Content = viewModel,
                Title = viewModel.GetDialogWindowTitle(),
                FontSize = App.Current.MainWindow.FontSize,
                Owner = App.Current.MainWindow
            };

            viewModel.RequestDialogWindowClose += (result) => { window.DialogResult = result; window.Close(); };
            return window.ShowDialog();
        }

        public static void Initialize()
        {
            ViewModelServices.DialogWindow = new DialogWindowService();
        }
    }
}
