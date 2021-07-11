using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class ContextActionViewModel : BaseViewModel
    {
        public string Name { get; set; }
        public ICommand Command { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsCheckbox { get; set; }
        public bool IsChecked { get; set; }

        public override string ToString()
        {
            return IsSeparator ? "<< separator >>" : Name;
        }
    }
}
