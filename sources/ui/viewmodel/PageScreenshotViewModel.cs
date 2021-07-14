using MgAl2O4.Utils;
using System.Drawing;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class PageScreenshotViewModel : LocalizedViewModel
    {
        public enum Mode
        {
            Info,
            Hashes,
            Learn,
        }

        public enum AnalyzerState
        {
            Disabled,
            Warning,
            Error,
            Active,
        }

        public MainWindowViewModel MainWindow;
        public ScreenAnalyzer ScreenAnalyzer = new ScreenAnalyzer();

        public BulkObservableCollection<ImageHashDataModelProxy> Hashes { get; } = new BulkObservableCollection<ImageHashDataModelProxy>();
        public BulkObservableCollection<ImageCardDataViewModel> Cards { get; } = new BulkObservableCollection<ImageCardDataViewModel>();

        private ImageHashDataModelProxy unknownHash;
        public ImageHashDataModelProxy UnknownHash { get => unknownHash; set => PropertySetAndNotify(value, ref unknownHash); }

        public BulkObservableCollection<ContextActionViewModel> ContextActions { get; } = new BulkObservableCollection<ContextActionViewModel>();

        private bool isOverlayActive = false;
        public bool IsOverlayActive { get => isOverlayActive; set => PropertySetAndNotify(value, ref isOverlayActive); }

        private Mode activeMode = Mode.Info;
        public Mode ActiveMode { get => activeMode; set { PropertySetAndNotify(value, ref activeMode); OnPropertyChanged("ActiveModeSwitcherIdx"); } }
        public int ActiveModeSwitcherIdx => (int)ActiveMode;

        private string descAnalyzerState;
        public string DescAnalyzerState { get => descAnalyzerState; set => PropertySetAndNotify(value, ref descAnalyzerState); }

        private AnalyzerState currentAnalyzerState;
        public AnalyzerState CurrentAnalyzerState { get => currentAnalyzerState; set => PropertySetAndNotify(value, ref currentAnalyzerState); }

        private IImageHashMatch selectedLearnMatch;
        public IImageHashMatch SelectedLearnMatch { get => selectedLearnMatch; set => PropertySetAndNotify(value, ref selectedLearnMatch); }

        private int numUnknownHashes = 0;
        public int NumUnknownHashes { get => numUnknownHashes; set { PropertySetAndNotify(value, ref numUnknownHashes); OnPropertyChanged("MainForm_Screenshot_Learn_PendingPlural"); } }

        public bool HasAnyHashes => Hashes.Count > 0;
        public bool HasAnyCards => Cards.Count > 0;

        public ICommand CommandToggleOverlay { get; private set; }
        public ICommand CommandRemoveLocalHashes { get; private set; }
        public ICommand CommandBuildContextActions { get; private set; }
        public ICommand CommandLearnHash { get; private set; }
        public ICommand CommandLearnDiscardAll { get; private set; }


        public string MainForm_Dynamic_Screenshot_SelectDetectionMatch => loc.strings.MainForm_Dynamic_Screenshot_SelectDetectionMatch;
        public string MainForm_Screenshot_CurrentState => loc.strings.MainForm_Screenshot_CurrentState;
        public string MainForm_Screenshot_History_CardsColumnDetection => loc.strings.MainForm_Screenshot_History_CardsColumnDetection;
        public string MainForm_Screenshot_History_CardsColumnSides => loc.strings.MainForm_Screenshot_History_CardsColumnSides;
        public string MainForm_Screenshot_History_CardsColumnType => loc.strings.MainForm_Screenshot_History_CardsColumnType;
        public string MainForm_Screenshot_History_HashColumnDetection => loc.strings.MainForm_Screenshot_History_HashColumnDetection;
        public string MainForm_Screenshot_History_HashColumnType => loc.strings.MainForm_Screenshot_History_HashColumnType;
        public string MainForm_Screenshot_InfoLines => loc.strings.MainForm_Screenshot_InfoLines;
        public string MainForm_Screenshot_Learn_DetectList => loc.strings.MainForm_Screenshot_Learn_DetectList;
        public string MainForm_Screenshot_Learn_DiscardAllButton => loc.strings.MainForm_Screenshot_Learn_DiscardAllButton;
        public string MainForm_Screenshot_Learn_DiscardAllInfo => loc.strings.MainForm_Screenshot_Learn_DiscardAllInfo;
        public string MainForm_Screenshot_Learn_PendingPlural => string.Format(loc.strings.MainForm_Screenshot_Learn_PendingPlural, ScreenAnalyzer.unknownHashes.Count - 1);
        public string MainForm_Screenshot_Learn_PendingSingular => loc.strings.MainForm_Screenshot_Learn_PendingSingular;
        public string MainForm_Screenshot_Learn_SaveButton => loc.strings.MainForm_Screenshot_Learn_SaveButton;
        public string MainForm_Screenshot_Learn_SourceImage => loc.strings.MainForm_Screenshot_Learn_SourceImage;
        public string MainForm_Screenshot_Learn_Type => loc.strings.MainForm_Screenshot_Learn_Type;
        public string MainForm_Screenshot_ListHint => loc.strings.MainForm_Screenshot_ListHint;
        public string MainForm_Screenshot_RemovePatternsButton => loc.strings.MainForm_Screenshot_RemovePatternsButton;
        public string MainForm_Screenshot_RemovePatternsTitle => loc.strings.MainForm_Screenshot_RemovePatternsTitle;

        public PageScreenshotViewModel()
        {
            // design time only
        }

        public PageScreenshotViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;

            CommandToggleOverlay = new RelayCommand<object>(CommandToggleOverlayFunc);
            CommandRemoveLocalHashes = new RelayCommand<object>(CommandRemoveLocalHashesFunc, (_) => PlayerSettingsDB.Get().customHashes.Count > 0);
            CommandBuildContextActions = new RelayCommand<object>(CommandBuildContextActionsFunc);
            CommandLearnHash = new RelayCommand<object>(CommandLearnHashFunc, (_) => SelectedLearnMatch != null);
            CommandLearnDiscardAll = new RelayCommand<object>(CommandLearnDiscardAllFunc);
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();

            UpdateAnalyzerDesc();
            ContextActions.Clear();

            foreach (var entry in Hashes)
            {
                entry.RefreshLocalization();
            }

            foreach (var entry in Cards)
            {
                entry.RefreshLocalization();
            }

            UnknownHash?.RefreshLocalization();
        }

        public void RequestDebugScreenshot(bool useCachedOnly)
        {
            ScreenAnalyzer.EMode mode = ScreenAnalyzer.EMode.Debug | (useCachedOnly ? ScreenAnalyzer.EMode.DebugScreenshotOnly : ScreenAnalyzer.EMode.None);

            //ScreenAnalyzer.unknownHashes.Clear();
            //ScreenAnalyzer.currentHashMatches.Clear();

            ScreenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.ScanAll | ScreenAnalyzer.EMode.DebugSaveMarkup);

            var state = ScreenAnalyzer.GetCurrentState();
            if (state == ScreenAnalyzer.EState.NoErrors)
            {
                Rectangle clipBounds = ScreenAnalyzer.scannerTriad.GetTimerScanBox();
                if (clipBounds.Width > 0)
                {
                    ScreenAnalyzer.scanClipBounds = clipBounds;
                    ScreenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.ScanTriad | ScreenAnalyzer.EMode.NeverResetCache, (int)ScannerTriad.EScanMode.TimerOnly);
                    ScreenAnalyzer.scanClipBounds = Rectangle.Empty;
                }
            }

            MainWindow.Overlay.UpdateScreenState(true);
            UpdateState();
        }

        private void CommandToggleOverlayFunc(object dummyParam)
        {
            IsOverlayActive = !IsOverlayActive;
            ViewModelServices.OverlayWindow.SetOverlayActive(MainWindow.Overlay, IsOverlayActive);

            if (IsOverlayActive)
            {
                ScreenAnalyzer.InitializeScreenData();
                MainWindow.Overlay.OnOverlayActive();
            }

            UpdateAnalyzerDesc();
        }

        private void CommandRemoveLocalHashesFunc(object dummyParam)
        {
            ImageHashDB.Get().Load();

            PlayerSettingsDB.Get().customHashes.Clear();
            PlayerSettingsDB.Get().MarkDirty();

            ScreenAnalyzer.ClearKnownHashes();
            ScreenAnalyzer.scannerTriad.cachedCardState.Clear();

            UpdateState();
        }

        private void CommandLearnHashFunc(object dummyParam)
        {
            if (ScreenAnalyzer.unknownHashes.Count > 0 && selectedLearnMatch != null)
            {
                ImageHashData hashData = UnknownHash.hashData;
                hashData.ownerOb = selectedLearnMatch.GetMatchOwner();

                PlayerSettingsDB.Get().AddKnownHash(hashData);

                ScreenAnalyzer.PopUnknownHash();
                NumUnknownHashes = ScreenAnalyzer.unknownHashes.Count;
                SelectedLearnMatch = null;

                UpdateState();
            }
        }

        private void CommandLearnDiscardAllFunc(object dummyParam)
        {
            ScreenAnalyzer.ClearAll();
            NumUnknownHashes = ScreenAnalyzer.unknownHashes.Count;

            UpdateState();
        }

        public void UpdateAnalyzerDesc()
        {
            var showState = ScreenAnalyzer.GetCurrentState();
            var newAnalyzerState = AnalyzerState.Error;

            if (IsOverlayActive || showState == ScreenAnalyzer.EState.UnknownHash)
            {
                switch (showState)
                {
                    case ScreenAnalyzer.EState.NoInputImage:
                        switch (ScreenAnalyzer.screenReader.currentState)
                        {
                            case ScreenReader.EState.MissingGameProcess: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingGameProcess; break;
                            case ScreenReader.EState.MissingGameWindow: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingGameWindow; break;
                            default: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_NoInputImage; break;
                        }
                        break;

                    case ScreenAnalyzer.EState.NoScannerMatch:
                        DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_NoScannerMatch;
                        break;

                    case ScreenAnalyzer.EState.UnknownHash:
                        DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenshot_Status_UnknownHash;
                        newAnalyzerState = AnalyzerState.Warning;
                        break;

                    case ScreenAnalyzer.EState.ScannerErrors:
                        DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_ScannerErrors;
                        if (ScreenAnalyzer.activeScanner is ScannerTriad)
                        {
                            switch (ScreenAnalyzer.scannerTriad.cachedScanError)
                            {
                                case ScannerTriad.EScanError.MissingGrid: DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenshot_Status_MissingGridOrCards; break;
                                case ScannerTriad.EScanError.MissingCards: DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenshot_Status_MissingGridOrCards; break;
                                case ScannerTriad.EScanError.FailedCardMatching: DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenshot_Status_FailedCardMatching; break;
                                default: break;
                            }
                        }
                        break;

                    case ScreenAnalyzer.EState.NoErrors:
                        DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenhot_Status_NoErrors;
                        newAnalyzerState = AnalyzerState.Active;
                        break;

                    default:
                        DescAnalyzerState = "??";
                        break;
                }
            }
            else
            {
                DescAnalyzerState = loc.strings.MainForm_Dynamic_Screenshot_Status_Disabled;
                newAnalyzerState = AnalyzerState.Disabled;
            }

            CurrentAnalyzerState = newAnalyzerState;
        }

        public void UpdateState()
        {
            UpdateAnalyzerDesc();

            var showState = ScreenAnalyzer.GetCurrentState();
            if ((showState == ScreenAnalyzer.EState.UnknownHash) && (ScreenAnalyzer.unknownHashes.Count > 0))
            {
                UnknownHash = new ImageHashDataModelProxy(ScreenAnalyzer.unknownHashes[0]);
                NumUnknownHashes = ScreenAnalyzer.unknownHashes.Count;
                ActiveMode = Mode.Learn;
            }
            else if ((ScreenAnalyzer.currentHashMatches.Count > 0) || (ScreenAnalyzer.scannerTriad.cachedCardState.Count > 0))
            {
                UpdateStateHashes();
                ActiveMode = Mode.Hashes;
            }
            else
            {
                ActiveMode = Mode.Info;
            }
        }

        private void UpdateStateHashes()
        {
            Hashes.SuspendNotifies();
            Hashes.Clear();
            foreach (var hashData in ScreenAnalyzer.currentHashMatches)
            {
                if ((hashData.type != EImageHashType.CardNumber) && (hashData.type != EImageHashType.CardImage))
                {
                    Hashes.Add(new ImageHashDataModelProxy(hashData));
                }
            }

            Hashes.ResumeNotifies();
            OnPropertyChanged("HasAnyHashes");

            Cards.SuspendNotifies();
            Cards.Clear();
            ScreenAnalyzer.scannerTriad.cachedCardState.Sort();
            foreach (var cardState in ScreenAnalyzer.scannerTriad.cachedCardState)
            {
                if ((cardState.state != ScannerTriad.ECardState.None) && (cardState.state != ScannerTriad.ECardState.Hidden))
                {
                    Cards.Add(new ImageCardDataViewModel(cardState));
                }
            }

            Cards.ResumeNotifies();
            OnPropertyChanged("HasAnyCards");
        }

        private void CommandBuildContextActionsFunc(object param)
        {
            ContextActions.SuspendNotifies();

            if (param is ImageHashDataModelProxy)
            {
                // hash: adjust & delete
                if (ContextActions.Count != 2)
                {
                    ContextActions.Clear();

                    ContextActions.Add(new ContextActionViewModel()
                    {
                        Name = loc.strings.MainForm_CtxMenu_Learn_Adjust,
                        Command = new RelayCommand<object>(x => CommandAdjustImageHash(x as ImageHashDataModelProxy))
                    });

                    ContextActions.Add(new ContextActionViewModel()
                    {
                        Name = loc.strings.MainForm_CtxMenu_Learn_Delete,
                        Command = new RelayCommand<object>(x => CommandDeleteImageHash(x as ImageHashDataModelProxy), x => CanDeleteImageHash(x as ImageHashDataModelProxy))
                    });
                }
            }
            else
            {
                // card: adjust only
                if (ContextActions.Count != 1)
                {
                    ContextActions.Clear();

                    ContextActions.Add(new ContextActionViewModel()
                    {
                        Name = loc.strings.MainForm_CtxMenu_Learn_Adjust,
                        Command = new RelayCommand<object>(x => CommandAdjustImageCard(x as ImageCardDataViewModel), x => CanAdjustImageCard(x as ImageCardDataViewModel))
                    });
                }
            }

            ContextActions.ResumeNotifies();
        }

        private void CommandAdjustImageHash(ImageHashDataModelProxy hashVM)
        {
            var editVM = new AdjustHashViewModel() { HashProxy = hashVM };
            var result = ViewModelServices.DialogWindow.ShowDialog(editVM);
            if (result ?? false)
            {
                Logger.WriteLine("Adjust: {0} => {1}", hashVM.NameLocalized, editVM.SelectedMatch.NameLocalized);

                PlayerSettingsDB.Get().RemoveKnownHash(hashVM.hashData);
                hashVM.hashData.ownerOb = editVM.SelectedMatch.GetMatchOwner();
                hashVM.hashData.matchDistance = 0;
                hashVM.hashData.isAuto = false;

                PlayerSettingsDB.Get().AddKnownHash(hashVM.hashData);
                UpdateState();
            }
        }

        private bool CanDeleteImageHash(ImageHashDataModelProxy hashVM)
        {
            return (hashVM != null) && (hashVM.hashData.matchDistance == 0) && !hashVM.hashData.isAuto;
        }

        private void CommandDeleteImageHash(ImageHashDataModelProxy hashVM)
        {
            Logger.WriteLine("Delete hash: {0}", hashVM.NameLocalized);

            PlayerSettingsDB.Get().RemoveKnownHash(hashVM.hashData);

            // remove from screen analyzer and update list to show result of user actions
            ScreenAnalyzer.currentHashMatches.Remove(hashVM.hashData);
            UpdateState();
        }

        private bool CanAdjustImageCard(ImageCardDataViewModel cardVM)
        {
            return (cardVM != null) && (cardVM.cardState.state != ScannerTriad.ECardState.Hidden) && (cardVM.cardState.state != ScannerTriad.ECardState.None);
        }

        private void CommandAdjustImageCard(ImageCardDataViewModel cardVM)
        {
            var orgName = cardVM.NameLocalized;
            var editVM = new AdjustCardViewModel(cardVM);
            var result = ViewModelServices.DialogWindow.ShowDialog(editVM);
            if (result ?? false)
            {
                bool hasChanges = false;

                // check modified numbers
                int[] AdjustedNum = { editVM.SideUp, editVM.SideLeft, editVM.SideDown, editVM.SideRight };
                for (int idx = 0; idx < 4; idx++)
                {
                    if (cardVM.cardState.sideNumber[idx] != AdjustedNum[idx])
                    {
                        Logger.WriteLine("Adjust: {0} => {1} ([{2}]: {3} => {4})",
                            orgName, editVM.SelectedMatch.NameLocalized,
                            idx,
                            cardVM.cardState.sideNumber[idx], AdjustedNum[idx]);

                        ImageHashData digitPattern = new ImageHashData() { type = EImageHashType.CardNumber, previewBounds = cardVM.cardState.sideInfo[idx].scanBox, previewContextBounds = cardVM.cardState.scanBox, isKnown = true };
                        digitPattern.CalculateHash(cardVM.cardState.sideInfo[idx].hashValues);
                        digitPattern.ownerOb = AdjustedNum[idx];

                        PlayerSettingsDB.Get().RemoveKnownHash(digitPattern);
                        if (AdjustedNum[idx] != cardVM.cardState.sideInfo[idx].matchNum)
                        {
                            PlayerSettingsDB.Get().AddKnownHash(digitPattern);
                            cardVM.cardState.sideInfo[idx].hasOverride = true;
                        }

                        cardVM.cardState.sideNumber[idx] = AdjustedNum[idx];
                        cardVM.cardState.card = editVM.SelectedMatch.cardOb;
                        cardVM.cardState.failedMatching = false;
                        hasChanges = true;
                    }
                }

                // check multicard hash
                if (editVM.HasMultipleMatches && cardVM.cardState.cardImageHash != null && cardVM.cardState.card != editVM.SelectedMatch.cardOb)
                {
                    Logger.WriteLine("Adjust: {0} => {1} (image hash)", orgName, editVM.SelectedMatch.NameLocalized);
                    cardVM.cardState.cardImageHash.ownerOb = editVM.SelectedMatch.cardOb;

                    PlayerSettingsDB.Get().RemoveKnownHash(cardVM.cardState.cardImageHash);
                    PlayerSettingsDB.Get().AddKnownHash(cardVM.cardState.cardImageHash);

                    cardVM.cardState.card = editVM.SelectedMatch.cardOb;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    UpdateStateHashes();
                }
            }
        }
    }
}
