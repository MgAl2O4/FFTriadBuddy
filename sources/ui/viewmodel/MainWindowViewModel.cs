using MgAl2O4.Utils;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FFTriadBuddy.UI
{
    public class MainWindowViewModel : LocalizedViewModel
    {
        public enum PageType
        {
            Setup,
            Screenshot,
            Simulate,
            Cards,
            Npcs,
            Info,
        }

        public PageSetupViewModel PageSetup { get; }
        public PageScreenshotViewModel PageScreenshot { get; }
        public PageSimulateViewModel PageSimulate { get; }
        public PageCardsViewModel PageCards { get; }
        public PageNpcsViewModel PageNpcs { get; }
        public PageInfoViewModel PageInfo { get; }
        public OverlayWindowViewModel Overlay { get; }

        public TriadGameModel GameModel;

        private int activePageIndex = 0;
        public int ActivePageIndex { get => activePageIndex; set => PropertySetAndNotify(value, ref activePageIndex); }

        private bool isUpdateNotifyVisible = false;
        public bool IsUpdateNotifyVisible { get => isUpdateNotifyVisible; set => PropertySetAndNotify(value, ref isUpdateNotifyVisible); }

        public BitmapImage LanguageFlag => IconDB.Get().mapFlags[LocResourceManager.Get().UserCultureCode];
        public ICommand CommandChangeLanguage { get; private set; }
        public ICommand CommandHideUpdateNotify { get; private set; }
        public ICommand CommandDebugScreenshot { get; private set; }

        public string WindowTitle => string.Format("{0}: {1} [{2}]", loc.strings.App_Title, GameModel != null ? GameModel.Npc.Name.GetLocalized() : "??", descTitleVersion);
        private int descTitleVersion = 0;

        public string MainForm_Cards_Title => loc.strings.MainForm_Cards_Title;
        public string MainForm_Info_Title => loc.strings.MainForm_Info_Title;
        public string MainForm_Npcs_Title => loc.strings.MainForm_Npcs_Title;
        public string MainForm_Screenshot_Title => loc.strings.MainForm_Screenshot_Title;
        public string MainForm_Setup_Title => loc.strings.MainForm_Setup_Title;
        public string MainForm_Simulate_Title => loc.strings.MainForm_Simulate_Title;
        public string MainForm_UpdateNotify => loc.strings.MainForm_UpdateNotify;

        public MainWindowViewModel()
        {
            GameModel = new TriadGameModel();
            GameModel.OnNpcChanged += (npc) => OnPropertyChanged("WindowTitle");

            PageSetup = new PageSetupViewModel(this);
            PageScreenshot = new PageScreenshotViewModel(this);
            PageSimulate = new PageSimulateViewModel(this);
            PageCards = new PageCardsViewModel(this);
            PageNpcs = new PageNpcsViewModel(this);
            PageInfo = new PageInfoViewModel();
            Overlay = new OverlayWindowViewModel(this);

            CommandChangeLanguage = new RelayCommand<object>(SwitchToNextLanguage);
            CommandHideUpdateNotify = new RelayCommand<object>((o) => IsUpdateNotifyVisible = false);
            CommandDebugScreenshot = new RelayCommand<object>((_) => PageSimulate.SpecialRules.RequestRuleDebug());

            var version = Assembly.GetEntryAssembly().GetName().Version;
            descTitleVersion = version.Major;

            LocalizationDB.OnLanguageChanged += RefreshLocalization;
            RefreshLocalization();

            RunUpdateCheck();
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            OnPropertyChanged("LanguageFlag");

            PageSetup.RefreshLocalization();
            PageScreenshot.RefreshLocalization();
            PageSimulate.RefreshLocalization();
            PageCards.RefreshLocalization();
            PageNpcs.RefreshLocalization();
            PageInfo.RefreshLocalization();
            Overlay.RefreshLocalization();
        }

        private void SwitchToNextLanguage(object dummyParam)
        {
            LocResourceManager locManager = LocResourceManager.Get();
            string[] cultureCodes = locManager.SupportedCultureCodes.ToArray();

            int currentIdx = Array.IndexOf(cultureCodes, locManager.UserCultureCode);
            int nextValidIdx = (currentIdx < 0) ? 0 : ((currentIdx + 1) % cultureCodes.Length);

            string newCultureCode = cultureCodes[nextValidIdx];

            PlayerSettingsDB.Get().forcedLanguage = newCultureCode;
            LocalizationDB.SetCurrentUserLanguage(newCultureCode);
        }

        public void SwitchToPage(PageType page)
        {
            ActivePageIndex = (int)page;
        }

        private void RunUpdateCheck()
        {
            Task updateTask = new Task(() =>
            {
                bool bFoundUpdate = GithubUpdater.FindAndDownloadUpdates(out string statusMsg);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Logger.WriteLine("Version check: " + statusMsg);
                    IsUpdateNotifyVisible = bFoundUpdate;
                });
            });

            updateTask.Start();
        }
    }
}
