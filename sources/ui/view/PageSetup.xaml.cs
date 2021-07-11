using System.Windows.Controls;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for PageSetup.xaml
    /// </summary>
    public partial class PageSetup : UserControl
    {
        public PageSetup()
        {
            InitializeComponent();
        }

        private void SearchableComboBox_SelectionEffectivelyChanged(object sender, object obj)
        {
            var pageVM = DataContext as PageSetupViewModel;
            var npcProxy = obj as NpcModelProxy;

            if (pageVM.CommandPickNpc.CanExecute(npcProxy))
            {
                pageVM.CommandPickNpc.Execute(npcProxy);
            }
        }
    }
}
