using System;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class FavDeckEditViewModel : LocalizedViewModel, IDialogWindowViewModel
    {
        public DeckViewModel FavDeck { get; set; }

        public string FavDeckForm_Info => loc.strings.FavDeckForm_Info;
        public string FavDeckForm_Name => loc.strings.FavDeckForm_Name;
        public string AdjustForm_CancelButton => loc.strings.AdjustForm_CancelButton;
        public string AdjustForm_SaveButton => loc.strings.AdjustForm_SaveButton;

        public ICommand CommandSave { get; private set; }
        public event Action<bool?> RequestDialogWindowClose;

        public FavDeckEditViewModel()
        {
            CommandSave = new RelayCommand<object>((_) => RequestDialogWindowClose.Invoke(true), (_) => FavDeck?.Name.Length > 0);
        }

        public string GetDialogWindowTitle()
        {
            return loc.strings.FavDeckForm_Title;
        }
    }
}
