using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class CardCollectionViewModel : LocalizedViewModel
    {
        private BulkObservableCollection<CardViewModel> cards = new BulkObservableCollection<CardViewModel>();
        public BulkObservableCollection<CardViewModel> Cards => cards;

        private string name;
        public string Name { get => name; set => PropertySetAndNotify(value, ref name); }

        public ICommand CommandSelect { get; protected set; }

        public CardCollectionViewModel()
        {
            CommandSelect = new RelayCommand<CardModelProxy>((cardInfo) => cardInfo.IsOwned = !cardInfo.IsOwned);
        }
    }
}
