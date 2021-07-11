using System;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class AdjustHashViewModel : LocalizedViewModel, IDialogWindowViewModel
    {
        public ImageHashDataModelProxy HashProxy { get; set; }

        public int MatchDistance => HashProxy.hashData.matchDistance;
        public string MatchDistanceInfo =>
            HashProxy.hashData.isAuto ? loc.strings.AdjustForm_Dynamic_Distance_Classifier :
            HashProxy.hashData.matchDistance == 0 ? loc.strings.AdjustForm_Dynamic_Distance_Exact :
            loc.strings.AdjustForm_Dynamic_Distance_DefaultHint;

        private IImageHashMatch selectedMatch;
        public IImageHashMatch SelectedMatch { get { UpdateSelectedMatch(); return selectedMatch; } set => PropertySetAndNotify(value, ref selectedMatch); }

        public ICommand CommandSave { get; private set; }
        public event Action<bool?> RequestDialogWindowClose;

        public string AdjustForm_Current => loc.strings.AdjustForm_Current;
        public string AdjustForm_Distance => loc.strings.AdjustForm_Distance;
        public string AdjustForm_HashList => loc.strings.AdjustForm_HashList;
        public string AdjustForm_CancelButton => loc.strings.AdjustForm_CancelButton;
        public string AdjustForm_SaveButton => loc.strings.AdjustForm_SaveButton;

        public AdjustHashViewModel()
        {
            CommandSave = new RelayCommand<object>((_) => RequestDialogWindowClose.Invoke(true), (_) => (HashProxy != null) && (selectedMatch != HashProxy.CurrentMatch));
        }

        public string GetDialogWindowTitle()
        {
            return loc.strings.AdjustForm_Title;
        }

        private void UpdateSelectedMatch()
        {
            if (selectedMatch == null && HashProxy != null)
            {
                selectedMatch = HashProxy.CurrentMatch;
            }
        }
    }
}
