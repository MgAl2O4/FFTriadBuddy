using MgAl2O4.GoogleAPI;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class Form1 : Form
    {
        private CardCtrl[] boardControls;
        private CardCtrl[] deckBlueControls;
        private CardCtrl highlightedCard;
        private CardGridCtrl[] cardGridControls;
        private FavDeckCtrl[] favControls;
        private static ImageList cardIconImages;
        private ListViewColumnSorter cardViewSorter;
        private ListViewColumnSorter npcViewSorter;
        private string orgTitle;
        private string versionTitle;
        private bool bSuspendSetupUpdates;
        private bool bSuspendNpcContextUpdates;
        private bool bSuspendCardContextUpdates;
        private bool bUseScreenReader;
        private bool bLoadedCloudSave;

        private TriadNpc currentNpc;
        private TriadDeck playerDeck;
        private TriadDeck playerDeckOptimizationTemp;
        private TriadGameSession gameSession;
        private TriadGameData gameState;
        private List<TriadGameData> gameUndoRed;
        private TriadGameData gameUndoBlue;
        private TriadDeckOptimizer deckOptimizer;
        private ScreenAnalyzer screenAnalyzer;
        private FormOverlay overlayForm;
        private MessageFilter scrollFilter;
        private FavDeckSolver[] favDeckSolvers;

        private static GoogleDriveService cloudStorage;

        private static Color gameHintLabelColor = Color.FromArgb(255, 193, 186);
        private static Color gameHintLabelHighlightColor = Color.FromArgb(0xce, 0x15, 0x00);
        private static Color screenshotStateActiveColor = Color.FromArgb(0x95, 0xfa, 0x87);
        private static Color screenshotStateFailureColor = Color.FromArgb(0xfa, 0x87, 0x95);
        private static Color screenshotStateWaitingColor = Color.FromArgb(0xfa, 0xec, 0x87);

        public Form1()
        {
            InitializeComponent();

            bSuspendSetupUpdates = false;
            bSuspendNpcContextUpdates = false;
            bSuspendCardContextUpdates = false;
            orgTitle = Text;

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            versionTitle = " [v" + version.Major + "]";
            Text = orgTitle + versionTitle;

            boardControls = new CardCtrl[9] { cardCtrl1, cardCtrl2, cardCtrl3, cardCtrl4, cardCtrl5, cardCtrl6, cardCtrl7, cardCtrl8, cardCtrl9 };
            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].SetImageLists(cardIconImages, imageListType, imageListRarity);
                boardControls[Idx].SetCard(null);
                boardControls[Idx].Tag = Idx;
            }

            deckBlueControls = new CardCtrl[5] { cardCtrlBlue1, cardCtrlBlue2, cardCtrlBlue3, cardCtrlBlue4, cardCtrlBlue5 };
            for (int Idx = 0; Idx < deckBlueControls.Length; Idx++)
            {
                deckBlueControls[Idx].SetImageLists(cardIconImages, imageListType, imageListRarity);
                deckBlueControls[Idx].SetCard(null);
                deckBlueControls[Idx].Tag = Idx;
                deckBlueControls[Idx].drawMode = ECardDrawMode.ImageOnly;
            }

            DeckCtrl[] deckControls = new DeckCtrl[] { deckCtrlSetup, deckCtrlSwapBlue, deckCtrlSwapRed, deckCtrlRandom };
            for (int Idx = 0; Idx < deckControls.Length; Idx++)
            {
                deckControls[Idx].SetImageLists(cardIconImages, imageListType, imageListRarity);
                deckControls[Idx].allowRearrange = false;
            }

            favControls = new FavDeckCtrl[] { favDeckCtrl1, favDeckCtrl2, favDeckCtrl3 };
            favDeckSolvers = new FavDeckSolver[favControls.Length];
            for (int Idx = 0; Idx < favControls.Length; Idx++)
            {
                favControls[Idx].SetImageLists(cardIconImages, imageListType, imageListRarity);
                favControls[Idx].SetDeck(null);
                favControls[Idx].Tag = Idx;
                favControls[Idx].OnEdit += favDeck_OnEdit;
                favControls[Idx].OnUse += favDeck_OnUse;
                favDeckSolvers[Idx] = new FavDeckSolver();
                favDeckSolvers[Idx].OnSolved += favDeck_OnSolved;
                favDeckSolvers[Idx].contextId = Idx;
            }

            deckCtrlSetup.clickAction = EDeckCtrlAction.Pick;
            deckCtrlSwapBlue.clickAction = EDeckCtrlAction.Highlight;
            deckCtrlSwapRed.clickAction = EDeckCtrlAction.Highlight;
            deckCtrlSwapBlue.deckOwner = ETriadCardOwner.Blue;
            deckCtrlSwapRed.deckOwner = ETriadCardOwner.Red;
            deckCtrlRandom.allowRearrange = true;
            deckCtrlRandom.clickAction = EDeckCtrlAction.Pick;

            CreateCardViewGrids();

            deckOptimizer = new TriadDeckOptimizer();
            deckOptimizer.OnFoundDeck += DeckOptimizer_OnFoundDeck;
            deckOptimizer.OnUpdateMaxSearchDecks += DeckOptimizer_OnUpdateMaxSearchDecks;

            screenAnalyzer = new ScreenAnalyzer();
            overlayForm = new FormOverlay();
            overlayForm.InitializeAssets(cardIconImages);
            overlayForm.screenAnalyzer = screenAnalyzer;
            overlayForm.OnUpdateState += OverlayForm_OnUpdateState;

            bUseScreenReader = false;
            RunUpdateCheck();
        }

        public class MessageFilter : IMessageFilter
        {
            private const int WM_MOUSEWHEEL = 0x020A;
            private const int WM_MOUSEHWHEEL = 0x020E;

            [DllImport("user32.dll")]
            static extern IntPtr WindowFromPoint(Point p);
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

            public bool PreFilterMessage(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_MOUSEWHEEL:
                    case WM_MOUSEHWHEEL:
                        IntPtr hControlUnderMouse = WindowFromPoint(new Point((int)m.LParam));
                        if (hControlUnderMouse == m.HWnd)
                        {
                            //Do nothing because it's already headed for the right control
                            return false;
                        }
                        else
                        {
                            //Send the scroll message to the control under the mouse
                            uint u = Convert.ToUInt32(m.Msg);
                            SendMessage(hControlUnderMouse, u, m.WParam, m.LParam);
                            return true;
                        }
                    default:
                        return false;
                }
            }
        }

        private void RunUpdateCheck()
        {
            Task updateTask = new Task(() =>
            {
                bool bFoundUpdate = GithubUpdater.FindAndDownloadUpdates(out string statusMsg);

                Invoke((MethodInvoker)delegate
                {
                    Logger.WriteLine("Version check: " + statusMsg);
                    labelUpdateNotify.Visible = bFoundUpdate;
                    labelUpdateNotify.BringToFront();
                });
            });
            updateTask.Start();
        }

        private void OverlayForm_OnUpdateState()
        {
            ShowScreenshotState();
        }

        public static bool InitializeGameAssets()
        {
            bool bResult = false;

            try
            {
                AssetManager assets = AssetManager.Get();
                if (assets.Init())
                {
                    bResult = TriadCardDB.Get().Load();
                    bResult = bResult && TriadNpcDB.Get().Load();
                    bResult = bResult && ImageHashDB.Get().Load();
                    bResult = bResult && TriadTournamentDB.Get().Load();

                    if (bResult)
                    {
                        bool bLoadedSettings = PlayerSettingsDB.Get().Load();
                        if (!bLoadedSettings)
                        {
                            Logger.WriteLine("Warning: failed to load player settings!");
                        }

                        TriadCardDB cardDB = TriadCardDB.Get();
                        cardIconImages = new ImageList
                        {
                            ImageSize = new Size(40, 40),
                            ColorDepth = ColorDepth.Depth32Bit
                        };

                        string nullImgPath = "icons/082500.png";
                        Image nullImg = Image.FromStream(assets.GetAsset(nullImgPath));

                        for (int Idx = 0; Idx < cardDB.cards.Count; Idx++)
                        {
                            if (cardDB.cards[Idx] != null)
                            {
                                string loadPath = "icons/" + cardDB.cards[Idx].IconPath;
                                Image loadedImage = Image.FromStream(assets.GetAsset(loadPath));
                                cardIconImages.Images.Add(loadedImage);
                            }
                            else
                            {
                                cardIconImages.Images.Add(nullImg);
                            }
                        }

                        cloudStorage = new GoogleDriveService(
                            GoogleClientIdentifiers.Keys,
                            new GoogleOAuth2.Token() { refreshToken = PlayerSettingsDB.Get().cloudToken });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Init failed: " + ex);
                bResult = false;
            }

            return bResult;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                if (tabControl1.SelectedTab == tabPageCards)
                {
                    contextMenuStripFindCard.Show(listViewCards, new Point(listViewCards.Size.Width, 0), ToolStripDropDownDirection.BelowLeft);
                }
                else if (tabControl1.SelectedTab == tabPageNpcs)
                {
                    contextMenuStripFindNpc.Show(listViewNpcs, new Point(listViewNpcs.Size.Width, 0), ToolStripDropDownDirection.BelowLeft);
                }
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                if (tabControl1.SelectedTab == tabPageCards)
                {
                    foreach (ListViewItem item in listViewCards.Items)
                    {
                        item.Selected = true;
                    }
                }
                else if (tabControl1.SelectedTab == tabPageNpcs)
                {
                    foreach (ListViewItem item in listViewNpcs.Items)
                    {
                        item.Selected = true;
                    }
                }
            }
            else if (e.KeyCode == Keys.F12)
            {
                if (tabControl1.SelectedTab == tabPagePlay)
                {
                    tabControlGameRules.SelectedTab = tabPageSubScreenshot;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            scrollFilter = new MessageFilter();
            Application.AddMessageFilter(scrollFilter);

            InitializeSetupUI();
            InitializeCardsUI();
            InitializeNpcUI();
            InitializeScreenshotUI();
            InitializeCloudStorage();
            UpdateFavDecks();

            // XInput polling
            if (PlayerSettingsDB.Get().useXInput)
            {
                XInputStub.StartPolling();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // XInput stop
            XInputStub.StopPolling();

            Application.RemoveMessageFilter(scrollFilter);
            scrollFilter = null;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            playerDB.UpdatePlayerDeckForNpc(currentNpc, playerDeck);
            playerDB.useAutoScan = (overlayForm != null) && overlayForm.IsUsingAutoScan();
            playerDB.cloudToken = (cloudStorage != null) ? cloudStorage.GetAuthToken().refreshToken : null;

            bool bShouldSaveInCloud = playerDB.isDirty;
            playerDB.Save();

            if (bShouldSaveInCloud && checkBoxUseCloudSaves.Checked && cloudStorage != null)
            {
                SaveCloudSettings();
            }
        }

        private void labelUpdateNotify_Click(object sender, EventArgs e)
        {
            labelUpdateNotify.Hide();
        }

        #region Tab: Setup

        class CardPickerOb
        {
            public readonly TriadCard Card;

            public CardPickerOb(TriadCard card)
            {
                Card = card;
            }

            public override string ToString()
            {
                return Card.Name;
            }
        }

        private void InitializeSetupUI()
        {
            bSuspendSetupUpdates = true;

            comboBoxNpc.Items.Clear();
            foreach (TriadNpc npc in TriadNpcDB.Get().npcs)
            {
                if (npc != null)
                {
                    comboBoxNpc.Items.Add(npc);
                }
            }

            comboBoxTournamentType.Items.Clear();
            foreach (TriadTournament tournament in TriadTournamentDB.Get().tournaments)
            {
                if (tournament != null)
                {
                    comboBoxTournamentType.Items.Add(tournament);
                }
            }

            if (comboBoxTournamentType.Items.Count > 0)
            {
                comboBoxTournamentType.SelectedIndex = 0;
            }

            List<TriadGameModifier> modObjects = new List<TriadGameModifier>();
            TriadGameModifier modNone = null;
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    if (type == typeof(TriadGameModifierRoulette))
                    {
                        // roulette is special, don't include in roulette resolve dropdowns
                        continue;
                    }

                    TriadGameModifier modNew = (TriadGameModifier)Activator.CreateInstance(type);
                    modObjects.Add(modNew);

                    if (type == typeof(TriadGameModifierNone))
                    {
                        modNone = modNew;
                    }
                }
            }
            modObjects.Sort();

            comboBoxRoulette1.Items.Clear();
            comboBoxRoulette2.Items.Clear();
            comboBoxRoulette3.Items.Clear();
            comboBoxRoulette4.Items.Clear();
            comboBoxRoulette1.Items.AddRange(modObjects.ToArray());
            comboBoxRoulette2.Items.AddRange(modObjects.ToArray());
            comboBoxRoulette3.Items.AddRange(modObjects.ToArray());
            comboBoxRoulette4.Items.AddRange(modObjects.ToArray());

            // roulette: each region combo needs to use different instance, so they can be resolved separately
            TriadGameModifier modRoulette = new TriadGameModifierRoulette();
            modObjects.Add(modRoulette);
            modObjects.Sort();

            comboBoxRegionRule1.Items.Clear();
            comboBoxRegionRule2.Items.Clear();
            comboBoxRegionRule1.Items.AddRange(modObjects.ToArray());

            int roulettePos = modObjects.IndexOf(modRoulette);
            modObjects[roulettePos] = new TriadGameModifierRoulette();

            comboBoxRegionRule2.Items.AddRange(modObjects.ToArray());
            comboBoxRegionRule1.SelectedItem = modNone;
            comboBoxRegionRule2.SelectedItem = modNone;

            // set npc - triggers game UI update & async evals
            bSuspendSetupUpdates = false;
            TriadNpc npcToSelect = TriadNpcDB.Get().Find("Triple Triad Master");
            comboBoxNpc.SelectedItem = npcToSelect;

            updateDeckState();
        }

        private void conditionalLockAtSetup(bool lockMe)
        {
            comboBoxNpc.Visible = !lockMe;
            progressBarNpc.Value = 0;
            progressBarNpc.Visible = lockMe;
            timerSelectNpc.Enabled = lockMe;

            foreach (FavDeckCtrl favSlot in favControls)
            {
                favSlot.SetLocked(lockMe);
            }
        }

        private void checkBoxSetupRules_CheckedChanged(object sender, EventArgs e)
        {
            tabControlSetupRules.SelectedTab = checkBoxSetupRules.Checked ? tabPageSetupRegion : tabPageSetupTournament;
            if (!bSuspendSetupUpdates)
            {
                updateGameUIAfterDeckChange();
            }
        }

        private void comboBoxNpc_SelectedIndexChanged(object sender, EventArgs e)
        {
            TriadNpc newSelectedNpc = (TriadNpc)comboBoxNpc.SelectedItem;
            if (newSelectedNpc != currentNpc)
            {
                PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                playerDB.UpdatePlayerDeckForNpc(currentNpc, playerDeck);

                currentNpc = newSelectedNpc;
                overlayForm.SetNpc(newSelectedNpc);

                TriadCard[] cardsCopy = null;
                if (playerDB.lastDeck.ContainsKey(currentNpc))
                {
                    TriadDeck savedDeck = PlayerSettingsDB.Get().lastDeck[currentNpc];
                    if (savedDeck != null && savedDeck.knownCards.Count == 5)
                    {
                        cardsCopy = savedDeck.knownCards.ToArray();
                    }
                }
                if (cardsCopy == null)
                {
                    cardsCopy = new TriadCard[5];
                    Array.Copy(PlayerSettingsDB.Get().starterCards, cardsCopy, cardsCopy.Length);
                }

                playerDeck = new TriadDeck(cardsCopy);
                deckCtrlSetup.SetDeck(playerDeck);

                updateGameUIAfterDeckChange();
            }
        }

        private void updateDeckState()
        {
            ETriadDeckState deckState = playerDeck.GetDeckState();
            string deckStateDesc =
                (deckState == ETriadDeckState.MissingCards) ? "Missing cards!" :
                (deckState == ETriadDeckState.HasDuplicates) ? "Found duplicate cards!" :
                (deckState == ETriadDeckState.TooMany4Star) ? "More than two 4+ star!" :
                (deckState == ETriadDeckState.TooMany5Star) ? "Only one 5 star allowed!" :
                "";

            labelDeckState.Text = deckStateDesc;
        }

        private void updateGameUIAfterDeckChange()
        {
            InitializeGameUI();
            overlayForm.UpdatePlayerDeck(playerDeck);
            PlayerSettingsDB.Get().UpdatePlayerDeckForNpc(currentNpc, playerDeck);

            string ruleDesc = "";
            foreach (TriadGameModifier mod in currentNpc.Rules)
            {
                if (mod.GetType() != typeof(TriadGameModifierNone))
                {
                    ruleDesc += mod.ToString() + ", ";
                }
            }

            labelLocation.Text = currentNpc.Location;
            labelLevel.Text = currentNpc.Deck.GetPower().ToString();
            labelDescChance.Text = labelChance.Text;
            labelDescRules.Text = (ruleDesc.Length > 2) ? ruleDesc.Remove(ruleDesc.Length - 2, 2) : "(none)";
            labelDescRules.Enabled = checkBoxSetupRules.Checked;
        }

        private async void buttonOptimize_Click(object sender, EventArgs e)
        {
            comboBoxNpc.Enabled = false;
            buttonOptimize.Visible = false;
            buttonOptimize.Enabled = false;
            buttonOptimizeAbort.Visible = true;
            buttonOptimizeAbort.Enabled = true;
            progressBarDeck.Visible = true;
            progressBarDeck.Value = 0;
            timerOptimizeDeck.Enabled = true;

            TriadGameModifier[] gameMods = new TriadGameModifier[] { (TriadGameModifier)comboBoxRegionRule1.SelectedItem, (TriadGameModifier)comboBoxRegionRule2.SelectedItem };
            List<TriadCard> lockedCards = deckCtrlSetup.GetLockedCards();

            string numOwnedStr = "0";
            string numPossibleStr = "0";
            deckOptimizer.PrepareStats(currentNpc, gameMods, lockedCards, out numOwnedStr, out numPossibleStr);

            labelOptNumOwned.Text = numOwnedStr;
            labelOptNumPossible.Text = numPossibleStr;
            labelOptNumTested.Text = "0";
            labelOptProgress.Text = "0%";
            tabControlSetupDetails.SelectedTab = tabPageSetupOptimizerStats;

            await deckOptimizer.Process(currentNpc, gameMods, lockedCards);

            timerOptimizationDeckUpdate.Enabled = false;
            timerOptimizeDeck_Tick(null, null);
            comboBoxNpc.Enabled = true;
            buttonOptimizeAbort.Visible = false;
            buttonOptimizeAbort.Enabled = false;
            buttonOptimize.Visible = true;
            buttonOptimize.Enabled = true;
            progressBarDeck.Visible = false;
            timerOptimizeDeck.Enabled = false;
            labelOptProgress.Text = deckOptimizer.IsAborted() ? ("aborted at " + deckOptimizer.GetProgress() + "%") : "100%";
            labelOptTimeLeft.Text = "--";

            playerDeck = deckOptimizer.optimizedDeck;

            if (playerDeck != null && playerDeck.knownCards.Count == 5)
            {
                deckCtrlSetup.SetDeck(playerDeck);
            }

            updateDeckState();
            updateGameUIAfterDeckChange();
            timerSetupDetails.Start();
        }

        private void timerSetupDetails_Tick(object sender, EventArgs e)
        {
            timerSetupDetails.Stop();
            tabControlSetupDetails.SelectedTab = tabPageFavDecks;
        }

        private void buttonOptimizeAbort_Click(object sender, EventArgs e)
        {
            deckOptimizer.AbortProcess();
        }

        private void DeckOptimizer_OnUpdateMaxSearchDecks(string numPossibleDesc)
        {
            BeginInvoke((MethodInvoker)delegate () { labelOptNumPossible.Text = numPossibleDesc; });
        }

        private void timerOptimizeDeck_Tick(object sender, EventArgs e)
        {
            progressBarDeck.Value = deckOptimizer.GetProgress();
            labelOptNumTested.Text = deckOptimizer.GetNumTestedDesc();
            labelOptProgress.Text = progressBarDeck.Value + "%";

            int secondsRemaining = deckOptimizer.GetSecondsRemaining(timerOptimizeDeck.Interval);
            TimeSpan tspan = TimeSpan.FromSeconds(secondsRemaining);
            if (tspan.Hours > 0 || tspan.Minutes > 55)
            {
                labelOptTimeLeft.Text = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", tspan.Hours, tspan.Minutes, tspan.Seconds);
            }
            else if (tspan.Minutes > 0 || tspan.Seconds > 55)
            {
                labelOptTimeLeft.Text = string.Format("{0:D2}m:{1:D2}s", tspan.Minutes, tspan.Seconds);
            }
            else
            {
                labelOptTimeLeft.Text = string.Format("{0:D2}s", tspan.Seconds);
            }
        }

        private void comboBoxRegionRule1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!bSuspendSetupUpdates)
            {
                updateGameUIAfterDeckChange();
            }
        }

        private void comboBoxRegionRule2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!bSuspendSetupUpdates)
            {
                updateGameUIAfterDeckChange();
            }
        }

        private void comboBoxTournamentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string ruleDesc = "";

            TriadTournament currentTournament = (TriadTournament)comboBoxTournamentType.SelectedItem;
            if (currentTournament != null)
            {
                foreach (TriadGameModifier mod in currentTournament.Rules)
                {
                    if (mod.GetType() != typeof(TriadGameModifierNone))
                    {
                        ruleDesc += mod.ToString() + ", ";
                    }
                }
            }

            labelTournamentRules.Text = (ruleDesc.Length > 2) ? ruleDesc.Remove(ruleDesc.Length - 2, 2) : "(none)";

            if (!bSuspendSetupUpdates)
            {
                InitializeGameUI();
            }
        }

        private void deckCtrlSetup_OnCardChanged(TriadCardInstance cardInst, int slotIdx, bool previewOnly)
        {
            bool bIsOwned = PlayerSettingsDB.Get().ownedCards.Contains(cardInst.card);
            cardInst.owner = bIsOwned ? ETriadCardOwner.Blue : ETriadCardOwner.Red;

            if (!previewOnly)
            {
                updateDeckState();
                updateGameUIAfterDeckChange();
            }
        }

        private void deckCtrlSetup_OnDeckRearranged(int slotIdx1, int slotIdx2)
        {
            bool bShouldRebuild = false;
            foreach (TriadGameModifier mod in gameSession.modifiers)
            {
                bShouldRebuild = bShouldRebuild || mod.IsDeckOrderImportant();
            }

            Logger.WriteLine("Player deck rearranged: [" + slotIdx1 + "] <-> [" + slotIdx2 + "]" + (bShouldRebuild ? " => rebuild!" : ""));
            if (bShouldRebuild)
            {
                updateGameUIAfterDeckChange();
            }
        }

        private void DeckOptimizer_OnFoundDeck(TriadDeck foundDeck)
        {
            playerDeckOptimizationTemp = foundDeck;

            // delayed update to buffer multiple changes
            if (!timerOptimizationDeckUpdate.Enabled)
            {
                Invoke((MethodInvoker)delegate
                {
                    timerOptimizationDeckUpdate.Start();
                });
            }
        }

        private void timerOptimizationDeckUpdate_Tick(object sender, EventArgs e)
        {
            timerOptimizationDeckUpdate.Enabled = false;

            playerDeck = playerDeckOptimizationTemp;
            if (playerDeck != null && playerDeck.knownCards.Count == 5)
            {
                deckCtrlSetup.SetDeck(playerDeck);
            }

            updateDeckState();
            updateGameUIAfterDeckChange();
        }

        private void UpdateFavDecks()
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            int numSaved = playerDB.favDecks.Count;
            int numToShow = Math.Min(numSaved, favControls.Length);

            for (int Idx = 0; Idx < numToShow; Idx++)
            {
                favControls[Idx].SetDeck(playerDB.favDecks[Idx]);
                favDeckSolvers[Idx].SetDeck(playerDB.favDecks[Idx]);
            }

            for (int Idx = numToShow; Idx < favControls.Length; Idx++)
            {
                favControls[Idx].SetDeck(null);
                favDeckSolvers[Idx].SetDeck(null);
            }

            buttonAddFav.Visible = numToShow < favControls.Length;
            buttonAddFav.Enabled = buttonAddFav.Visible;
        }

        private void UpdateFavDeckSolvers()
        {
            if (favDeckSolvers != null)
            {
                for (int Idx = 0; Idx < favDeckSolvers.Length; Idx++)
                {
                    favDeckSolvers[Idx].Update(gameSession, currentNpc);
                }
            }
        }

        private void buttonAddFav_Click(object sender, EventArgs e)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            FormFavEdit editForm = new FormFavEdit();
            editForm.InitDeck(playerDB.favDecks.Count, playerDeck, cardIconImages, imageListType, imageListRarity);
            editForm.ShowDialog();

            UpdateFavDecks();
        }

        private void favDeck_OnEdit(int slotIdx)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            FormFavEdit editForm = new FormFavEdit();
            editForm.InitDeck(slotIdx, null, cardIconImages, imageListType, imageListRarity);
            editForm.ShowDialog();

            UpdateFavDecks();
        }

        private void favDeck_OnUse(int slotIdx)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            TriadCard[] cardsCopy = new TriadCard[5];
            TriadDeckNamed useDeck = playerDB.favDecks[slotIdx];
            Array.Copy(useDeck.knownCards.ToArray(), cardsCopy, cardsCopy.Length);

            playerDeck = new TriadDeck(cardsCopy);
            deckCtrlSetup.SetDeck(playerDeck);

            updateGameUIAfterDeckChange();
        }

        private void favDeck_OnSolved(int id, TriadGameResultChance chance)
        {
            Invoke((MethodInvoker)delegate
            {
                favControls[id].UpdateChance(chance);
            });
        }

        #endregion

        #region Tab: Cards

        private void InitializeCardsUI()
        {
            listViewCards.Items.Clear();

            cardViewSorter = new ListViewColumnSorter();
            listViewCards.ListViewItemSorter = cardViewSorter;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            TriadCardDB cardsDB = TriadCardDB.Get();

            foreach (TriadCard card in cardsDB.cards)
            {
                if (card == null)
                {
                    continue;
                }

                string rarityDesc = "*";
                for (int Idx = 0; Idx < (int)card.Rarity; Idx++)
                {
                    rarityDesc += " *";
                }

                string typeDesc = "";
                if (card.Type != ETriadCardType.None)
                {
                    typeDesc = card.Type.ToString();
                }

                string powerDesc = "[" + GetCardPowerDesc(card.Sides[(int)ETriadGameSide.Up]) + ", " +
                    GetCardPowerDesc(card.Sides[(int)ETriadGameSide.Left]) + ", " +
                    GetCardPowerDesc(card.Sides[(int)ETriadGameSide.Down]) + ", " +
                    GetCardPowerDesc(card.Sides[(int)ETriadGameSide.Right]) + "]";

                bool bIsOwned = playerDB.ownedCards.Contains(card);
                string ownedDesc = bIsOwned ? "Yes" : "";
                string sortOrder = card.SortOrder.ToString();

                ListViewItem cardListItem = new ListViewItem(new string[] { card.Name, rarityDesc, powerDesc, typeDesc, ownedDesc, sortOrder });
                cardListItem.Checked = bIsOwned;
                cardListItem.Tag = card;

                listViewCards.Items.Add(cardListItem);
            }

            cardViewSorter.SortColumn = columnHeaderSO.Index;
            listViewCards.Sort();
            labelNumOwned.Text = playerDB.ownedCards.Count.ToString();
        }

        private string GetCardPowerDesc(int number)
        {
            return (number == 10) ? "A" : number.ToString();
        }

        private void listViewCards_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == cardViewSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (cardViewSorter.Order == SortOrder.Ascending)
                {
                    cardViewSorter.Order = SortOrder.Descending;
                }
                else
                {
                    cardViewSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                cardViewSorter.SortColumn = e.Column;
                cardViewSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listViewCards.Sort();
        }

        private void listViewCards_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (!bSuspendCardContextUpdates)
            {
                TriadCard card = (TriadCard)e.Item.Tag;

                if (!TabControlNoTabs.bIsRoutingCreateMesage)
                {
                    PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                    playerDB.ownedCards.Remove(card);
                    playerDB.MarkDirty();

                    if (e.Item.Checked)
                    {
                        playerDB.ownedCards.Add(card);
                    }
                }

                bSuspendCardContextUpdates = true;
                updateOwnedCards(card);
                bSuspendCardContextUpdates = false;
            }
        }

        private void updateOwnedCards(TriadCard card)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            bool bIsOwned = (card != null) && playerDB.ownedCards.Contains(card);

            if (card == null) { bSuspendCardContextUpdates = true; }

            foreach (ListViewItem lvi in listViewCards.Items)
            {
                if (card == null)
                {
                    TriadCard cardTag = (TriadCard)lvi.Tag;
                    bIsOwned = playerDB.ownedCards.Contains(cardTag);

                    lvi.SubItems[4].Text = bIsOwned ? "Yes" : "";
                    lvi.BackColor = bIsOwned ? Color.FromArgb(0xb8, 0xfc, 0xd2) : SystemColors.Window;
                    lvi.Checked = bIsOwned;
                }
                else if (lvi.Tag == card)
                {
                    lvi.SubItems[4].Text = bIsOwned ? "Yes" : "";
                    lvi.BackColor = bIsOwned ? Color.FromArgb(0xb8, 0xfc, 0xd2) : SystemColors.Window;
                    lvi.Checked = bIsOwned;

                    break;
                }
            }

            if (card == null)
            {
                UpdateCardViewGrids();
            }
            else if (!TabControlNoTabs.bIsRoutingCreateMesage)
            {
                UpdateOwnedCardInGrids(card, bIsOwned);
            }

            labelNumOwned.Text = playerDB.ownedCards.Count.ToString();

            updateNpcRewards(card);
            updateDeckState();

            if (card == null) { bSuspendCardContextUpdates = false; }
        }

        private void contextMenuStripFindCard_Opened(object sender, EventArgs e)
        {
            toolStripFindCardText.Text = "";
            toolStripFindCardText.Focus();
        }

        private void toolStripFindCardText_KeyDown(object sender, KeyEventArgs e)
        {
            if (toolStripFindCardText.Text != "")
            {
                bool bCanSelect = true;
                for (int Idx = 0; Idx < listViewCards.Items.Count; Idx++)
                {
                    listViewCards.Items[Idx].Selected = false;

                    if (bCanSelect &&
                        listViewCards.Items[Idx].Text.StartsWith(toolStripFindCardText.Text, StringComparison.InvariantCultureIgnoreCase))
                    {
                        listViewCards.EnsureVisible(Idx);
                        listViewCards.Items[Idx].Selected = true;
                        bCanSelect = false;
                    }
                }
            }

            if (e.KeyCode == Keys.Return)
            {
                contextMenuStripFindCard.Hide();
            }
        }

        private void CreateCardViewGrids()
        {
            TriadCardDB cardDB = TriadCardDB.Get();
            int numCards = cardDB.cards.Count;
            int numGrids = ((numCards + 29) / 30) + 1;

            cardGridControls = new CardGridCtrl[numGrids];
            for (int GridIdx = 0; GridIdx < numGrids; GridIdx++)
            {
                CardGridCtrl gridCtrl = new CardGridCtrl();
                gridCtrl.SetImageLists(cardIconImages, imageListType, imageListRarity);
                gridCtrl.Tag = GridIdx;
                gridCtrl.InitCardControls();
                gridCtrl.OnCardClicked += GridCtrl_OnCardClicked;
                cardGridControls[GridIdx] = gridCtrl;
            }

            UpdateCardViewGrids();
        }

        private void GridCtrl_OnCardClicked(TriadCard card)
        {
            if (!bSuspendCardContextUpdates)
            {
                PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                playerDB.MarkDirty();
                bool bWasOwned = playerDB.ownedCards.Contains(card);
                if (bWasOwned)
                {
                    playerDB.ownedCards.Remove(card);
                }
                else
                {
                    playerDB.ownedCards.Add(card);
                }

                bSuspendCardContextUpdates = true;
                updateOwnedCards(card);
                bSuspendCardContextUpdates = false;
            }
        }

        private void UpdateCardViewGrids()
        {
            bool bOnlyOwned = false;

            List<TriadCard> cardList = new List<TriadCard>();
            cardList.AddRange(bOnlyOwned ? PlayerSettingsDB.Get().ownedCards : TriadCardDB.Get().cards);
            cardList.RemoveAll(x => (x == null || !x.IsValid()));
            cardList.Sort((c1, c2) => { return c1.SortOrder.CompareTo(c2.SortOrder); });

            flowLayoutPanelCardGrids.Controls.Clear();
            int GridIdx = -1;
            int NumInGrid = 30;
            bool wasExPage = false;

            for (int CardIdx = 0; CardIdx < cardList.Count; CardIdx++)
            {
                bool isExPage = cardList[CardIdx].SortOrder >= 1000;
                if (NumInGrid == 30 || (!wasExPage && isExPage))
                {
                    GridIdx++;
                    NumInGrid = 0;

                    flowLayoutPanelCardGrids.Controls.Add(cardGridControls[GridIdx]);
                    cardGridControls[GridIdx].Clear();
                }

                bool bIsCardOwned = bOnlyOwned ? true : PlayerSettingsDB.Get().ownedCards.Contains(cardList[CardIdx]);
                cardGridControls[GridIdx].SetCard(NumInGrid, cardList[CardIdx], bIsCardOwned);

                NumInGrid++;
                wasExPage = isExPage;
            }
        }

        private void UpdateOwnedCardInGrids(TriadCard card, bool bOwned)
        {
            bool bOnlyOwned = false;
            if (bOnlyOwned)
            {
                // full rebuild required
                UpdateCardViewGrids();
            }
            else
            {
                foreach (CardGridCtrl gridCtrl in cardGridControls)
                {
                    gridCtrl.UpdateOwnedCard(card, bOwned);
                }
            }
        }

        private void contextMenuStripCardInfo_Opened(object sender, EventArgs e)
        {
            ListViewItem selectedCardItem = (listViewCards.SelectedItems.Count > 0) ? listViewCards.SelectedItems[0] : null;
            if (selectedCardItem != null)
            {
                TriadCard contextCard = (TriadCard)selectedCardItem.Tag;
                contextMenuStripCardInfo.Tag = contextCard;

                int LastIdxToRemove = contextMenuStripCardInfo.Items.Count - 3;
                for (int Idx = LastIdxToRemove; Idx >= 1; Idx--)
                {
                    contextMenuStripCardInfo.Items.RemoveAt(Idx);
                }

                List<TriadNpc> rewardNpc = TriadNpcDB.Get().FindByReward(contextCard);
                if (rewardNpc.Count > 0)
                {
                    foreach (TriadNpc npc in rewardNpc)
                    {
                        ToolStripMenuItem npcDescItem = new ToolStripMenuItem(npc.Name);
                        npcDescItem.Tag = npc;
                        npcDescItem.Click += OnCardInfoNpcClicked;

                        contextMenuStripCardInfo.Items.Insert(contextMenuStripCardInfo.Items.Count - 2, npcDescItem);
                    }
                }
                else
                {
                    ToolStripMenuItem npcDescItem = new ToolStripMenuItem("(none)");
                    npcDescItem.Enabled = false;

                    contextMenuStripCardInfo.Items.Insert(contextMenuStripCardInfo.Items.Count - 2, npcDescItem);
                }
            }
        }

        private void OnCardInfoNpcClicked(object sender, EventArgs e)
        {
            ToolStripMenuItem senderMenuItem = (ToolStripMenuItem)sender;
            tabControl1.SelectedTab = tabPageNpcs;

            for (int Idx = 0; Idx < listViewNpcs.Items.Count; Idx++)
            {
                if (listViewNpcs.Items[Idx].Tag == senderMenuItem.Tag)
                {
                    listViewNpcs.SelectedIndices.Clear();
                    listViewNpcs.Items[Idx].Selected = true;
                    listViewNpcs.EnsureVisible(Idx);
                    break;
                }
            }
        }

        private void toolStripMenuFindCardOnline_Click(object sender, EventArgs e)
        {
            TriadCard contextCard = (TriadCard)contextMenuStripCardInfo.Tag;
            if (contextCard != null)
            {
                System.Diagnostics.Process.Start("https://triad.raelys.com/cards/" + contextCard.Id);
            }
        }

        #endregion

        #region Tab: Npcs

        private void InitializeNpcUI()
        {
            listViewNpcs.Items.Clear();

            npcViewSorter = new ListViewColumnSorter();
            listViewNpcs.ListViewItemSorter = npcViewSorter;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            TriadNpcDB npcsDB = TriadNpcDB.Get();

            foreach (TriadNpc npc in npcsDB.npcs)
            {
                if (npc == null)
                {
                    continue;
                }

                string ruleDesc = "";
                foreach (TriadGameModifier mod in npc.Rules)
                {
                    ruleDesc += mod + ", ";
                }

                ruleDesc = (ruleDesc.Length > 2) ? ruleDesc.Remove(ruleDesc.Length - 2, 2) : "(none)";

                bool bIsCompleted = playerDB.completedNpcs.Contains(npc);
                string completedDesc = bIsCompleted ? "Yes" : "";

                string rewardDesc = "";
                foreach (TriadCard card in npc.Rewards)
                {
                    if (!playerDB.ownedCards.Contains(card))
                    {
                        rewardDesc += card.Name + ", ";
                    }
                }

                rewardDesc = (rewardDesc.Length > 2) ? rewardDesc.Remove(rewardDesc.Length - 2, 2) : "";
                string deckPowerDesc = npc.Deck.GetPower().ToString();

                ListViewItem npcListItem = new ListViewItem(new string[] { npc.Name, deckPowerDesc, npc.Location, ruleDesc, completedDesc, rewardDesc });
                npcListItem.Checked = bIsCompleted;
                npcListItem.Tag = npc;

                listViewNpcs.Items.Add(npcListItem);
            }

            listViewNpcs.Sort();
            labelNumPendingNpc.Text = (npcsDB.npcs.Count - playerDB.completedNpcs.Count).ToString();
        }

        private void updateNpcRewards(TriadCard changedCard)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            foreach (ListViewItem lvi in listViewNpcs.Items)
            {
                TriadNpc npc = lvi.Tag as TriadNpc;
                if (npc != null && (npc.Rewards.Contains(changedCard) || changedCard == null))
                {
                    string rewardDesc = "";
                    foreach (TriadCard card in npc.Rewards)
                    {
                        if (!playerDB.ownedCards.Contains(card))
                        {
                            rewardDesc += card.Name + ", ";
                        }
                    }

                    rewardDesc = (rewardDesc.Length > 2) ? rewardDesc.Remove(rewardDesc.Length - 2, 2) : "";
                    lvi.SubItems[5].Text = rewardDesc;
                }
            }
        }

        private void listViewNpcs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == npcViewSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (npcViewSorter.Order == SortOrder.Ascending)
                {
                    npcViewSorter.Order = SortOrder.Descending;
                }
                else
                {
                    npcViewSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                npcViewSorter.SortColumn = e.Column;
                npcViewSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listViewNpcs.Sort();
        }

        private void listViewNpcs_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            TriadNpc npc = (TriadNpc)e.Item.Tag;
            bool bIsCompleted = e.Item.Checked;

            e.Item.SubItems[4].Text = bIsCompleted ? "Yes" : "";
            e.Item.BackColor = bIsCompleted ? Color.FromArgb(0xb8, 0xfc, 0xd2) : SystemColors.Window;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            playerDB.completedNpcs.Remove(npc);
            playerDB.MarkDirty();

            if (bIsCompleted)
            {
                playerDB.completedNpcs.Add(npc);
            }

            labelNumPendingNpc.Text = (TriadNpcDB.Get().npcs.Count - playerDB.completedNpcs.Count).ToString();
        }

        private void contextMenuStripFindNpc_Opened(object sender, EventArgs e)
        {
            toolStripFindNpcText.Text = "";
            toolStripFindNpcText.Focus();
        }

        private void contextMenuStripSelectNpc_Opened(object sender, EventArgs e)
        {
            ListViewItem selectedNpcItem = (listViewNpcs.SelectedItems.Count > 0) ? listViewNpcs.SelectedItems[0] : null;
            if (selectedNpcItem != null)
            {
                TriadNpc contextNpc = (TriadNpc)selectedNpcItem.Tag;
                contextMenuStripSelectNpc.Tag = contextNpc;
                bSuspendNpcContextUpdates = true;

                PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                int numRewards = (contextNpc != null) ? contextNpc.Rewards.Count : 0;

                ToolStripMenuItem[] rewardItems = new ToolStripMenuItem[4] { npcReward1ToolStripMenuItem, npcReward2ToolStripMenuItem, npcReward3ToolStripMenuItem, npcReward4ToolStripMenuItem };
                for (int Idx = 0; Idx < rewardItems.Length; Idx++)
                {
                    rewardItems[Idx].Tag = Idx;
                    if (numRewards > Idx)
                    {
                        bool bIsRewardOwned = playerDB.ownedCards.Contains(contextNpc.Rewards[Idx]);

                        rewardItems[Idx].Visible = true;
                        rewardItems[Idx].Text = contextNpc.Rewards[Idx].Name;
                        rewardItems[Idx].Checked = bIsRewardOwned;
                    }
                    else
                    {
                        rewardItems[Idx].Visible = false;
                    }
                }

                bSuspendNpcContextUpdates = false;
            }
        }

        private void contextMenuStripSelectNpc_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void npcRewardToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (!bSuspendNpcContextUpdates)
            {
                ToolStripMenuItem senderItem = (ToolStripMenuItem)sender;
                TriadNpc contextNpc = (TriadNpc)contextMenuStripSelectNpc.Tag;
                int rewardIdx = (int)senderItem.Tag;

                TriadCard card = contextNpc.Rewards[rewardIdx];
                PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                playerDB.MarkDirty();
                playerDB.ownedCards.Remove(card);

                if (senderItem.Checked)
                {
                    playerDB.ownedCards.Add(card);
                }

                updateOwnedCards(card);
            }
        }

        private void toolStripFindNpcText_KeyDown(object sender, KeyEventArgs e)
        {
            if (toolStripFindNpcText.Text != "")
            {
                bool bCanSelect = true;
                for (int Idx = 0; Idx < listViewNpcs.Items.Count; Idx++)
                {
                    listViewNpcs.Items[Idx].Selected = false;

                    if (bCanSelect &&
                        listViewNpcs.Items[Idx].Text.StartsWith(toolStripFindNpcText.Text, StringComparison.InvariantCultureIgnoreCase))
                    {
                        listViewNpcs.EnsureVisible(Idx);
                        listViewNpcs.Items[Idx].Selected = true;
                        bCanSelect = false;
                    }
                }
            }

            if (e.KeyCode == Keys.Return)
            {
                contextMenuStripFindNpc.Hide();
            }
        }

        private void selectNpcToPlayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TriadNpc npcToSelect = (TriadNpc)contextMenuStripSelectNpc.Tag;
            comboBoxNpc.SelectedItem = npcToSelect;

            contextMenuStripSelectNpc.Close();
        }

        #endregion

        #region Tab: Play

        private void InitializeGameUI()
        {
            if ((currentNpc != null) && (playerDeck != null))
            {
                gameSession = new TriadGameSession();
                if (checkBoxSetupRules.Checked)
                {
                    gameSession.modifiers.AddRange(currentNpc.Rules);
                }
                else
                {
                    TriadTournament currentTournament = (TriadTournament)comboBoxTournamentType.SelectedItem;
                    if (currentTournament != null)
                    {
                        gameSession.modifiers.AddRange(currentTournament.Rules);
                    }
                }

                gameSession.modifiers.Add((TriadGameModifier)comboBoxRegionRule1.SelectedItem);
                gameSession.modifiers.Add((TriadGameModifier)comboBoxRegionRule2.SelectedItem);
                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    mod.OnMatchInit();
                }

                gameSession.UpdateSpecialRules();
                if (bUseScreenReader) { gameState.resolvedSpecial = gameSession.specialRules; }

                updateGameRulesDesc();
                Text = orgTitle + ": " + currentNpc.Name + versionTitle;

                UpdateFavDeckSolvers();
                ResetGame();
            }
        }

        private void updateGameRulesDesc()
        {
            string ruleDesc = "";
            foreach (TriadGameModifier mod in gameSession.modifiers)
            {
                if (mod.GetType() != typeof(TriadGameModifierNone))
                {
                    ruleDesc += mod.ToString() + ", ";
                }
            }

            labelRules.Text = (ruleDesc.Length > 0) ? ruleDesc.Remove(ruleDesc.Length - 2, 2) : "(none)";
        }

        private void showSpecialRuleUI(ETriadGameSpecialMod specialMod, bool bSpecialActive)
        {
            TriadDeckInstanceManual blueDeckEx = gameState.deckBlue as TriadDeckInstanceManual;
            TriadDeckInstanceManual redDeckEx = gameState.deckRed as TriadDeckInstanceManual;
            if (blueDeckEx == null || redDeckEx == null)
            {
                return;
            }

            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].Visible = !bSpecialActive;
            }

            labelSpecialRules.Visible = bSpecialActive;
            buttonReset.Enabled = !bSpecialActive;
            switch (specialMod)
            {
                case ETriadGameSpecialMod.RandomizeRule:
                    ComboBox[] rouletteCombos = new ComboBox[] { comboBoxRoulette1, comboBoxRoulette2, comboBoxRoulette3, comboBoxRoulette4 };
                    Label[] rouletteLabels = new Label[] { labelRouletteDesc1, labelRouletteDesc2, labelRouletteDesc3, labelRouletteDesc4 };
                    for (int Idx = 0; Idx < 4; Idx++)
                    {
                        rouletteCombos[Idx].Visible = false;
                        rouletteLabels[Idx].Visible = false;
                        rouletteCombos[Idx].SelectedIndex = -1;
                    }

                    int rouletteIdx = 0;
                    foreach (TriadGameModifier mod in gameSession.modifiers)
                    {
                        if (mod.GetType() == typeof(TriadGameModifierRoulette))
                        {
                            TriadGameModifierRoulette rouletteMod = (TriadGameModifierRoulette)mod;
                            rouletteCombos[rouletteIdx].Tag = rouletteMod;
                            rouletteCombos[rouletteIdx].Visible = true;
                            rouletteLabels[rouletteIdx].Visible = true;
                            rouletteIdx++;
                        }
                    }

                    buttonConfirmRuleRoulette.Enabled = false;
                    tabControlGameRules.SelectedTab = tabPageSubRoulette;
                    break;

                case ETriadGameSpecialMod.RandomizeBlueDeck:
                    tabControlGameRules.SelectedTab = tabPageSubRandom;
                    TriadDeck randomizedBlueDeck = new TriadDeck(blueDeckEx.deck.knownCards);
                    gameState.deckBlue = new TriadDeckInstanceManual(randomizedBlueDeck);
                    deckCtrlRandom.SetDeck(randomizedBlueDeck);
                    break;

                case ETriadGameSpecialMod.SwapCards:
                    tabControlGameRules.SelectedTab = tabPageSubSwap;
                    deckCtrlSwapBlue.SetDeck(blueDeckEx.deck);
                    deckCtrlSwapRed.SetDeck(redDeckEx.deck);
                    deckCtrlSwap_OnCardSelected(null, 0);
                    break;

                default:
                    tabControlGameRules.SelectedTab = tabPageSubGame;
                    break;
            }
        }

        private void buttonConfirmRuleRoulette_Click(object sender, EventArgs e)
        {
            gameState.resolvedSpecial |= ETriadGameSpecialMod.RandomizeRule;
            gameSession.UpdateSpecialRules();
            updateGameRulesDesc();
            ShowGameData(gameState);
        }

        private void buttonConfirmRuleRandom_Click(object sender, EventArgs e)
        {
            gameState.bDebugRules = true;
            TriadGameModifierRandom.StaticRandomized(gameState);
            gameState.bDebugRules = false;

            gameState.resolvedSpecial |= ETriadGameSpecialMod.RandomizeBlueDeck;
            ShowGameData(gameState);
        }

        private void buttonConfirmRuleSwap_Click(object sender, EventArgs e)
        {
            TriadCard swapFromBlue = null;
            TriadCard swapFromRed = null;
            int slotIdxBlue = 0;
            int slotIdxRed = 0;

            deckCtrlSwapBlue.GetActiveCard(out slotIdxBlue, out swapFromBlue);
            deckCtrlSwapRed.GetActiveCard(out slotIdxRed, out swapFromRed);

            gameState.bDebugRules = true;
            TriadGameModifierSwap.StaticSwapCards(gameState, swapFromBlue, slotIdxBlue, swapFromRed, slotIdxRed);
            gameState.bDebugRules = false;

            gameState.resolvedSpecial |= ETriadGameSpecialMod.SwapCards;
            ShowGameData(gameState);
        }

        private void buttonConfirmRuleScreenshot_Click(object sender, EventArgs e)
        {
            ScreenAnalyzer.EMode mode = ScreenAnalyzer.EMode.Debug;
            if (checkBoxDebugScreenshotForceCached.Checked)
            {
                mode |= ScreenAnalyzer.EMode.DebugScreenshotOnly;
            }

            screenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.ScanAll);

            Rectangle clipBounds = screenAnalyzer.scannerTriad.GetTimerScanBox();
            if (clipBounds.Width > 0)
            {
                screenAnalyzer.scanClipBounds = clipBounds;
                screenAnalyzer.DoWork(mode | ScreenAnalyzer.EMode.ScanTriad, (int)ScannerTriad.EScanMode.TimerOnly);
                screenAnalyzer.scanClipBounds = Rectangle.Empty;
            }

            overlayForm.UpdateScreenState(true);

            ShowScreenshotState();
            ShowGameData(gameState);
        }

        private void deckCtrlSwap_OnCardSelected(TriadCardInstance cardInst, int slotIdx)
        {
            bool bCanSwap = deckCtrlSwapBlue.HasActiveCard() && deckCtrlSwapRed.HasActiveCard();
            buttonConfirmRuleSwap.Enabled = bCanSwap;
            buttonConfirmRuleSwap.Text = bCanSwap ? "Apply Rule" : "Select one card from each deck to swap";
        }

        private void ShowGameData(TriadGameData gameData)
        {
            ETriadGameSpecialMod pendingUIRules = ETriadGameSpecialMod.None;
            if ((gameData.numCardsPlaced == 0) && (gameSession.specialRules != ETriadGameSpecialMod.None))
            {
                ETriadGameSpecialMod specialUIMask = ETriadGameSpecialMod.RandomizeRule | ETriadGameSpecialMod.RandomizeBlueDeck | ETriadGameSpecialMod.SwapCards;

                pendingUIRules = ((gameSession.specialRules & specialUIMask) & ~gameData.resolvedSpecial);
                if (pendingUIRules != ETriadGameSpecialMod.None)
                {
                    for (int Idx = 0; Idx < 32; Idx++)
                    {
                        ETriadGameSpecialMod testRule = (ETriadGameSpecialMod)(1 << Idx);
                        if (testRule > pendingUIRules)
                        {
                            break;
                        }

                        if ((testRule & pendingUIRules) != ETriadGameSpecialMod.None)
                        {
                            showSpecialRuleUI(testRule, true);
                            break;
                        }
                    }
                }
                else
                {
                    showSpecialRuleUI(ETriadGameSpecialMod.None, false);
                }
            }

            if (pendingUIRules == ETriadGameSpecialMod.None && tabControlGameRules.SelectedTab != tabPageSubGame)
            {
                showSpecialRuleUI(ETriadGameSpecialMod.None, false);
            }

            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].SetCard(gameData.board[Idx]);
            }

            TriadDeckInstanceManual blueDeckEx = gameData.deckBlue as TriadDeckInstanceManual;
            if (blueDeckEx != null)
            {
                for (int Idx = 0; Idx < deckBlueControls.Length; Idx++)
                {
                    bool bIsPlaced = blueDeckEx.IsPlaced(Idx);
                    TriadCard blueCardToShow = (!bIsPlaced && (Idx < blueDeckEx.deck.knownCards.Count)) ? blueDeckEx.deck.knownCards[Idx] : null;

                    if (deckBlueControls[Idx].GetCard() != blueCardToShow)
                    {
                        deckBlueControls[Idx].SetCard((blueCardToShow != null) ? new TriadCardInstance(blueCardToShow, ETriadCardOwner.Blue) : null);
                    }
                }
            }

            listViewRedDeck.Items.Clear();
            listViewRedDeck.SmallImageList = cardIconImages;

            List<TriadCard> redCards = gameData.deckRed.GetAvailableCards();
            foreach (TriadCard card in redCards)
            {
                ListViewItem cardListItem = new ListViewItem(card.Name, card.Id)
                {
                    ToolTipText = card.Name,
                    Tag = card
                };

                listViewRedDeck.Items.Add(cardListItem);
            }

            buttonReset.Text = (gameData.numCardsPlaced == 0) ? "Blue starts" : "Reset";
            buttonUndoRed.Enabled = (gameUndoRed.Count > 0);
            buttonUndoRed.Text = (gameData.numCardsPlaced == 0) ? "Red start: drag && drop card on board" : "Undo last Red move";

            if (gameState.state == ETriadGameState.BlueWins)
            {
                labelChance.Text = "yay!";
            }
            else if (gameState.state == ETriadGameState.BlueLost)
            {
                labelChance.Text = "lost";
            }
            else if (gameState.state == ETriadGameState.BlueDraw)
            {
                labelChance.Text = "draw";
            }

            bool bHasLastRedReminder = false;
            if (gameState.numCardsPlaced == (gameState.board.Length - 1) && gameState.state == ETriadGameState.InProgressRed)
            {
                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    bHasLastRedReminder = bHasLastRedReminder || mod.HasLastRedReminder();
                }
            }

            if (bHasLastRedReminder && !timerGameStateHint.Enabled)
            {
                labelGameStateHint.Text = "[Place last red card to trigger modifier]";
                labelGameStateHint.BackColor = gameHintLabelHighlightColor;
                timerGameStateHint.Enabled = true;
            }
            else if (!bHasLastRedReminder && timerGameStateHint.Enabled)
            {
                labelGameStateHint.Text = "[Drag && drop cards on board]";
                labelGameStateHint.BackColor = gameHintLabelColor;
                timerGameStateHint.Enabled = false;
            }
        }


        private void SetHighlightedCard(CardCtrl cardCtrl)
        {
            if (highlightedCard != null)
            {
                highlightedCard.SetHighlighted(false);
            }

            highlightedCard = cardCtrl;

            if (highlightedCard != null)
            {
                highlightedCard.SetHighlighted(true);
            }
        }

        private void PlayBlueCard()
        {
            if (gameState.state == ETriadGameState.InProgressBlue)
            {
                if ((gameSession.specialRules & ETriadGameSpecialMod.BlueCardSelection) != ETriadGameSpecialMod.None)
                {
                    toolTip1.ShowAlways = true;
                    toolTip1.Show("Click to change card", panelBlueDeck, 0, -10, 3000);
                    gameUndoBlue = new TriadGameData(gameState);
                }

                bool bHasMove = gameSession.SolverFindBestMove(gameState, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                if (bHasMove)
                {
                    Logger.WriteLine("Blue> [" + bestNextPos + "] " + bestNextCard.Name + ", " +
                        ((bestChance.expectedResult == ETriadGameState.BlueDraw) ? ("draw " + bestChance.drawChance.ToString("P2")) : ("win " + bestChance.winChance.ToString("P2"))));

                    gameState.bDebugRules = true;
                    gameSession.PlaceCard(gameState, bestNextCard, ETriadCardOwner.Blue, bestNextPos);
                    gameState.bDebugRules = false;

                    SetHighlightedCard(boardControls[bestNextPos]);
                    labelChance.Text = (bestChance.expectedResult == ETriadGameState.BlueDraw) ? (bestChance.drawChance.ToString("P2") + " (DRAW)") : bestChance.winChance.ToString("P2");
                }
                else
                {
                    SetHighlightedCard(null);
                    labelChance.Text = "0%";
                }

                ShowGameData(gameState);
            }
        }

        private void ResetGame()
        {
            Logger.WriteLine("Game reset. Npc:'" + currentNpc + "', rules:'" + labelRules.Text + "', blue:" + playerDeck);
            foreach (TriadGameModifier mod in gameSession.modifiers)
            {
                mod.OnMatchInit();
            }

            gameState = gameSession.StartGame(playerDeck, currentNpc.Deck, ETriadGameState.InProgressRed);
            gameUndoRed = new List<TriadGameData>();
            gameUndoBlue = null;
            conditionalLockAtSetup(true);

            labelChance.Text = "...";
            labelDescChance.Text = "...";

            Task.Run(() =>
            {
                bool bHasMove = gameSession.SolverFindBestMove(gameState, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);

                Invoke((MethodInvoker)delegate
                {
                    if (bHasMove)
                    {
                        labelChance.Text = (bestChance.expectedResult == ETriadGameState.BlueDraw) ? (bestChance.drawChance.ToString("P2") + " (DRAW)") : bestChance.winChance.ToString("P2");
                    }
                    else
                    {
                        labelChance.Text = "0%";
                    }

                    labelDescChance.Text = labelChance.Text;

                    ShowGameData(gameState);
                    conditionalLockAtSetup(false);
                });
            });
        }

        private void cardCtrlBlue_Click(object sender, EventArgs e)
        {
            if (gameState.state == ETriadGameState.InProgressRed && gameUndoBlue != null)
            {
                TriadDeckInstanceManual blueDeckEx = gameState.deckBlue as TriadDeckInstanceManual;
                if ((gameSession.specialRules & ETriadGameSpecialMod.BlueCardSelection) != ETriadGameSpecialMod.None && blueDeckEx != null)
                {
                    int deckSlotIdx = (int)(((CardCtrl)sender).Tag);
                    TriadCard newForcedCard = blueDeckEx.deck.knownCards[deckSlotIdx];

                    if (gameSession.forcedBlueCard != newForcedCard && !blueDeckEx.IsPlaced(deckSlotIdx))
                    {
                        Logger.WriteLine("Force blue card: " + newForcedCard.Name);
                        gameSession.forcedBlueCard = newForcedCard;

                        gameState = gameUndoBlue;
                        PlayBlueCard();
                    }
                }
            }
        }

        private void cardCtrl_DragDrop(object sender, DragEventArgs e)
        {
            int boardPos = (int)((CardCtrl)sender).Tag;
            TriadCard card = (TriadCard)e.Data.GetData(typeof(TriadCard));

            if (gameSession != null && gameState != null)
            {
                gameSession.forcedBlueCard = null;
                TriadGameData newUndoState = new TriadGameData(gameState);

                Logger.WriteLine("Red> [" + boardPos + "] " + card.Name);
                gameState.bDebugRules = true;
                bool bPlaced = gameSession.PlaceCard(gameState, card, ETriadCardOwner.Red, boardPos);
                gameState.bDebugRules = false;

                // additional debug logs
                {
                    int numBoardPlaced = 0;
                    int availBoardMask = 0;
                    for (int Idx = 0; Idx < gameState.board.Length; Idx++)
                    {
                        if (gameState.board[Idx] != null)
                        {
                            numBoardPlaced++;
                        }
                        else
                        {
                            availBoardMask |= (1 << Idx);
                        }
                    }

                    Logger.WriteLine("  Board cards:" + numBoardPlaced + " (" + availBoardMask.ToString("x") + "), placed:" + bPlaced);
                }

                if (bPlaced)
                {
                    PlayBlueCard();
                    gameUndoRed.Add(newUndoState);
                }
            }
        }

        private void cardCtrl_DragEnter(object sender, DragEventArgs e)
        {
            int boardPos = (int)((CardCtrl)sender).Tag;

            if (e.Data.GetDataPresent(typeof(TriadCard)) && gameState.board[boardPos] == null)
            {
                e.Effect = DragDropEffects.Move;

                TriadCard card = (TriadCard)e.Data.GetData(typeof(TriadCard));
                boardControls[boardPos].SetCard(new TriadCardInstance(card, ETriadCardOwner.Red));
            }
        }

        private void cardCtrl_DragLeave(object sender, EventArgs e)
        {
            int boardPos = (int)((CardCtrl)sender).Tag;
            if (gameState.board[boardPos] == null)
            {
                boardControls[boardPos].SetCard(null);
            }
        }

        private void listViewRedDeck_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(((ListViewItem)e.Item).Tag, DragDropEffects.Move);
        }

        private void listViewRedDeck_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TriadCard)))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void buttonUndoRed_Click(object sender, EventArgs e)
        {
            if (gameUndoRed.Count > 0)
            {
                gameState = gameUndoRed[gameUndoRed.Count - 1];
                gameUndoRed.RemoveAt(gameUndoRed.Count - 1);

                ShowGameData(gameState);
            }
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            if (gameState != null)
            {
                if (gameState.numCardsPlaced == 0)
                {
                    // blue starts
                    gameState.state = ETriadGameState.InProgressBlue;
                    PlayBlueCard();
                }
                else
                {
                    // reset
                    ResetGame();
                }
            }
        }

        private void timerSelectNpc_Tick(object sender, EventArgs e)
        {
            progressBarNpc.Value = gameSession.currentProgress;
        }

        private void timerGameStateHint_Tick(object sender, EventArgs e)
        {
            labelGameStateHint.BackColor = (labelGameStateHint.BackColor == gameHintLabelColor) ? gameHintLabelHighlightColor : gameHintLabelColor;
        }

        private void comboBoxRoulette_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool bAllSet = true;
            bAllSet = bAllSet && (!comboBoxRoulette1.Visible || (comboBoxRoulette1.SelectedIndex != -1));
            bAllSet = bAllSet && (!comboBoxRoulette2.Visible || (comboBoxRoulette2.SelectedIndex != -1));
            bAllSet = bAllSet && (!comboBoxRoulette3.Visible || (comboBoxRoulette3.SelectedIndex != -1));
            bAllSet = bAllSet && (!comboBoxRoulette4.Visible || (comboBoxRoulette4.SelectedIndex != -1));
            buttonConfirmRuleRoulette.Enabled = bAllSet;

            ComboBox senderCombo = (ComboBox)sender;
            if (senderCombo.SelectedItem != null)
            {
                TriadGameModifierRoulette rouletteMod = (TriadGameModifierRoulette)senderCombo.Tag;
                Type modifierType = senderCombo.SelectedItem.GetType();
                TriadGameModifier modNew = (TriadGameModifier)Activator.CreateInstance(modifierType);

                rouletteMod.SetRuleInstance(modNew);
            }
        }

        #endregion

        #region Tab: Screenshot
        private void InitializeScreenshotUI()
        {
            ShowScreenshotState();

            tabControlScreenDetection.SelectedTab = tabPageDetectionInfo;
        }

        class LocalHashComboItem : IComparable
        {
            public object SourceObject;
            string Description;

            public LocalHashComboItem(TriadGameModifier mod)
            {
                SourceObject = mod;
                Description = mod.ToString();
            }

            public LocalHashComboItem(TriadCard card)
            {
                SourceObject = card;
                Description = card.Name;
            }

            public LocalHashComboItem(CactpotNumberHash number)
            {
                SourceObject = number;
                Description = number.ToString();
            }

            public LocalHashComboItem(int number)
            {
                SourceObject = number;
                Description = (number == 10) ? "A" : number.ToString();
            }

            public int CompareTo(object obj)
            {
                return Description.CompareTo(obj.ToString());
            }

            public override string ToString()
            {
                return Description;
            }
        }

        private void ShowScreenshotState()
        {
            ScreenAnalyzer.EState showState = screenAnalyzer.GetCurrentState();
            Color useBackColor = screenshotStateFailureColor;

            if (bUseScreenReader || showState == ScreenAnalyzer.EState.UnknownHash)
            {
                switch (showState)
                {
                    case ScreenAnalyzer.EState.NoInputImage:
                        switch (screenAnalyzer.screenReader.currentState)
                        {
                            case ScreenReader.EState.MissingGameProcess: labelScreenshotState.Text = "Game is not running"; break;
                            case ScreenReader.EState.MissingGameWindow: labelScreenshotState.Text = "Can't find game window"; break;
                            default: labelScreenshotState.Text = "Can't retrieve image to analyze"; break;
                        }
                        break;

                    case ScreenAnalyzer.EState.NoScannerMatch:
                        labelScreenshotState.Text = "Can't find minigame window";
                        break;

                    case ScreenAnalyzer.EState.UnknownHash:
                        labelScreenshotState.Text = "Can't recognize pattern! See details below";
                        useBackColor = screenshotStateWaitingColor;
                        break;

                    case ScreenAnalyzer.EState.ScannerErrors:
                        labelScreenshotState.Text = "Failed to recognize minigame";
                        if (screenAnalyzer.activeScanner is ScannerTriad)
                        {
                            switch (screenAnalyzer.scannerTriad.cachedScanError)
                            {
                                case ScannerTriad.EScanError.MissingGrid: labelScreenshotState.Text = "Can't find board! Try resetting UI position, turning off reshade, etc..."; break;
                                case ScannerTriad.EScanError.MissingCards: labelScreenshotState.Text = "Can't find blue deck! Try resetting UI position, turning off reshade, etc..."; break;
                                case ScannerTriad.EScanError.FailedCardMatching: labelScreenshotState.Text = "Failed to recognize some of cards! Game state won't be accurate"; break;
                                default: break;
                            }
                        }
                        break;

                    case ScreenAnalyzer.EState.NoErrors:
                        labelScreenshotState.Text = "Active";
                        useBackColor = screenshotStateActiveColor;
                        break;

                    default:
                        labelScreenshotState.Text = "??";
                        break;
                }
            }
            else
            {
                labelScreenshotState.Text = "Disabled";
            }

            panelScreenshotState.BackColor = useBackColor;

            // keep outside bUseScreenReader scope for debug mode updates
            if ((showState == ScreenAnalyzer.EState.UnknownHash) && (screenAnalyzer.unknownHashes.Count > 0))
            {
                tabControlScreenDetection.SelectedTab = tabPageDetectionLearn;
                labelDeleteLastHint.Visible = false;
                labelLocalHashPending.Text = (screenAnalyzer.unknownHashes.Count > 1) ?
                    ("(there " + (screenAnalyzer.unknownHashes.Count == 2 ? "is" : "are") + " " + (screenAnalyzer.unknownHashes.Count - 1) + " more pending)") :
                    "";

                ImageHashData hashData = screenAnalyzer.unknownHashes[0].hashData;
                labelLocalHashType.Text = hashData.Type.ToString();
                pictureBoxLocalHash.Image = screenAnalyzer.unknownHashes[0].sourceImage;

                comboBoxLocalHash.SelectedIndex = -1;
                comboBoxLocalHash.Items.Clear();
                switch (hashData.Type)
                {
                    case EImageHashType.Rule:
                        foreach (TriadGameModifier mod in ImageHashDB.Get().modObjects)
                        {
                            comboBoxLocalHash.Items.Add(new LocalHashComboItem(mod));
                        }
                        break;

                    case EImageHashType.Card:
                        foreach (TriadCard card in TriadCardDB.Get().sameNumberMap[((TriadCard)hashData.GuideOb).SameNumberId])
                        {
                            comboBoxLocalHash.Items.Add(new LocalHashComboItem(card));
                        }
                        break;

                    case EImageHashType.Cactpot:
                        foreach (CactpotNumberHash number in CactpotGame.hashDB)
                        {
                            comboBoxLocalHash.Items.Add(new LocalHashComboItem(number));
                        }
                        break;

                    default: break;
                }

                comboBoxLocalHash.Text = "(select match)";
                comboBoxLocalHash_SelectedIndexChanged(null, null);

                if (tabControl1.SelectedTab != tabPageScreenshot)
                {
                    tabControl1.SelectTab(tabPageScreenshot);
                }
            }
            else
            {
                bool hasAnyDetection = (screenAnalyzer.currentHashDetections.Count > 0) || (screenAnalyzer.scannerTriad.cachedCardState.Count > 0);
                tabControlScreenDetection.SelectedTab = hasAnyDetection ? tabPageDetectionHistory : tabPageDetectionInfo;
                labelDeleteLastHint.Visible = true;
                pictureBoxLocalHash.Image = null;

                listViewDetectionHashes.Items.Clear();
                foreach (KeyValuePair<FastBitmapHash, int> kvp in screenAnalyzer.currentHashDetections)
                {
                    TriadGameModifier modOb = kvp.Key.GuideOb as TriadGameModifier;
                    TriadCard cardOb = kvp.Key.GuideOb as TriadCard;
                    CactpotNumberHash cactpotOb = kvp.Key.GuideOb as CactpotNumberHash;

                    ListViewItem lvi = new ListViewItem(kvp.Value == 0 ? "Exact" : "Similar");
                    lvi.Tag = kvp.Key;
                    lvi.SubItems.Add(
                        (modOb != null) ? "Rule: " + modOb.GetName() :
                        (cardOb != null) ? "Card: " + cardOb.ToShortString() :
                        (cactpotOb != null) ? "Cactpot: " + cactpotOb.ToString() :
                        "unknown type");

                    listViewDetectionHashes.Items.Add(lvi);
                }

                listViewDetectionCards.Items.Clear();
                screenAnalyzer.scannerTriad.cachedCardState.Sort();
                foreach (ScannerTriad.CardState cardState in screenAnalyzer.scannerTriad.cachedCardState)
                {
                    ListViewItem lvi = new ListViewItem(cardState.name);
                    lvi.Tag = cardState;

                    lvi.SubItems.Add((cardState.sideNumber == null) ? "" :
                        (cardState.sideNumber[0] + "-" + cardState.sideNumber[2] + "-" + cardState.sideNumber[1] + "-" + cardState.sideNumber[3]));
                    lvi.SubItems.Add(cardState.card == null ? "not detected!" : cardState.card.Name);

                    lvi.BackColor = (cardState.card == null) ? Color.MistyRose : SystemColors.Window;

                    listViewDetectionCards.Items.Add(lvi);
                }
            }

            buttonLocalHashRemove.Enabled = PlayerSettingsDB.Get().customHashes.Count > 0;
            labelDeleteLastHint.Text =
                (listViewDetectionHashes.Items.Count > 0) ? "^ Delete entry to learn again" :
                "";
        }

        private void checkBoxUseScreenshots_CheckedChanged(object sender, EventArgs e)
        {
            bUseScreenReader = checkBoxUseScreenshots.Checked;
            overlayForm.Visible = bUseScreenReader;

            if (bUseScreenReader)
            {
                screenAnalyzer.InitializeScreenData();

                overlayForm.InitOverlayLocation(Bounds);
                gameState.resolvedSpecial = gameSession.specialRules;
            }

            ShowScreenshotState();
            ShowGameData(gameState);

            if (PlayerSettingsDB.Get().useXInput)
            {
                overlayForm.SetXInputEnble(bUseScreenReader);
            }
        }

        private void buttonRemoveLocalHashes_Click(object sender, EventArgs e)
        {
            PlayerSettingsDB.Get().customHashes.Clear();
            PlayerSettingsDB.Get().MarkDirty();
            screenAnalyzer.currentHashDetections.Clear();
            ShowScreenshotState();
        }

        private void comboBoxLocalHash_SelectedIndexChanged(object sender, EventArgs e)
        {
            buttonLocalHashStore.Enabled = comboBoxLocalHash.SelectedIndex != -1;
        }

        private void buttonLocalHashStore_Click(object sender, EventArgs e)
        {
            if (screenAnalyzer != null && (screenAnalyzer.unknownHashes.Count > 0) && comboBoxLocalHash.SelectedItem != null)
            {
                LocalHashComboItem hashComboItem = (LocalHashComboItem)comboBoxLocalHash.SelectedItem;
                if (hashComboItem != null)
                {
                    ImageHashData hashData = new ImageHashData(hashComboItem.SourceObject, screenAnalyzer.unknownHashes[0].hashData.Hash, screenAnalyzer.unknownHashes[0].hashData.Type);
                    PlayerSettingsDB.Get().AddKnownHash(hashData);
                    screenAnalyzer.PopUnknownHash();

                    ShowScreenshotState();
                }
            }
        }

        private void listViewDetectionHistory_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Delete) && listViewDetectionHashes.SelectedIndices.Count == 1)
            {
                screenAnalyzer.RemoveKnownHash(listViewDetectionHashes.SelectedItems[0].Tag as FastBitmapHash);
                listViewDetectionHashes.Items.RemoveAt(listViewDetectionHashes.SelectedIndices[0]);
            }
        }

        private void panelScreenshotState_Click(object sender, EventArgs e)
        {
            checkBoxUseScreenshots.Checked = !checkBoxUseScreenshots.Checked;
        }

        private void listViewDetectionCards_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewItem lvi = listViewDetectionCards.GetItemAt(e.Location.X, e.Location.Y);
                if (lvi != null)
                {
                    ScannerTriad.CardState cardState = lvi.Tag as ScannerTriad.CardState;
                    if (cardState != null && cardState.sideNumber != null)
                    {
                        lvi.Selected = true;
                        contextMenuStripCardParser.Tag = cardState;

                        toolStripMenuItemCardParseTitle.Text = "Adjust detection for: " + cardState.name;
                        toolStripComboBoxCardParseUp.Text = (cardState.sideNumber[0] == 10) ? "A" : cardState.sideNumber[0].ToString();
                        toolStripComboBoxCardParseDown.Text = (cardState.sideNumber[1] == 10) ? "A" : cardState.sideNumber[1].ToString();
                        toolStripComboBoxCardParseLeft.Text = (cardState.sideNumber[2] == 10) ? "A" : cardState.sideNumber[2].ToString();
                        toolStripComboBoxCardParseRight.Text = (cardState.sideNumber[3] == 10) ? "A" : cardState.sideNumber[3].ToString();

                        contextMenuStripCardParser.Show(listViewDetectionCards, e.Location, ToolStripDropDownDirection.BelowRight);
                    }
                }
            }
        }

        private void toolStripComboBoxCard_TextChanged(object sender, EventArgs e)
        {
            bool bCanConfirm = false;
            string previewStr = "(none)";

            ScannerTriad.CardState cardState = contextMenuStripCardParser.Tag as ScannerTriad.CardState;
            if (cardState != null &&
                toolStripComboBoxCardParseUp.Text.Length == 1 &&
                toolStripComboBoxCardParseLeft.Text.Length == 1 &&
                toolStripComboBoxCardParseDown.Text.Length == 1 &&
                toolStripComboBoxCardParseRight.Text.Length == 1)
            {
                int numUp = 0;
                int numLeft = 0;
                int numDown = 0;
                int numRight = 0;

                if (!int.TryParse(toolStripComboBoxCardParseUp.Text, out numUp)) { if (toolStripComboBoxCardParseUp.Text.Equals("A", StringComparison.InvariantCultureIgnoreCase)) { numUp = 10; } }
                if (!int.TryParse(toolStripComboBoxCardParseLeft.Text, out numLeft)) { if (toolStripComboBoxCardParseLeft.Text.Equals("A", StringComparison.InvariantCultureIgnoreCase)) { numLeft = 10; } }
                if (!int.TryParse(toolStripComboBoxCardParseDown.Text, out numDown)) { if (toolStripComboBoxCardParseDown.Text.Equals("A", StringComparison.InvariantCultureIgnoreCase)) { numDown = 10; } }
                if (!int.TryParse(toolStripComboBoxCardParseRight.Text, out numRight)) { if (toolStripComboBoxCardParseRight.Text.Equals("A", StringComparison.InvariantCultureIgnoreCase)) { numRight = 10; } }

                cardState.adjustNumber = new int[4] { numUp, numDown, numLeft, numRight };

                TriadCard foundCard = TriadCardDB.Get().Find(numUp, numDown, numLeft, numRight);
                if (foundCard != null)
                {
                    if (foundCard.SameNumberId < 0)
                    {
                        previewStr = foundCard.Name;
                    }
                    else
                    {
                        foreach (TriadCard card in TriadCardDB.Get().sameNumberMap[foundCard.SameNumberId])
                        {
                            previewStr += card.Name + ", ";
                        }

                        previewStr = previewStr.Remove(previewStr.Length - 2, 2);
                    }

                    bCanConfirm = true;
                }
            }

            toolStripMenuItemCardParsePreview.Text = "Current match: " + previewStr;
            toolStripMenuItemCardParseConfirm.Enabled = bCanConfirm;
        }

        private void toolStripMenuItemCardParseConfirm_Click(object sender, EventArgs e)
        {
            ScannerTriad.CardState cardState = contextMenuStripCardParser.Tag as ScannerTriad.CardState;
            if (cardState != null && cardState.sideNumber != null && cardState.adjustNumber != null && cardState.sideImage != null)
            {
                Logger.WriteLine("Updating parser data for " + cardState.name);
                for (int Idx = 0; Idx < 4; Idx++)
                {
                    if (cardState.sideNumber[Idx] != cardState.adjustNumber[Idx])
                    {
                        Logger.WriteLine(">> new pattern for digit:" + (cardState.adjustNumber[Idx] == 10 ? "A" : cardState.adjustNumber[Idx].ToString()));
                        PlayerSettingsDB.Get().AddKnownDigit(new ImagePatternDigit(cardState.adjustNumber[Idx], cardState.sideImage[Idx].Pixels));
                    }
                }
            }
        }

        #endregion

        #region Cloud storage

        private void InitializeCloudStorage()
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            playerDB.OnUpdated += PlayerDB_OnUpdated;

            bLoadedCloudSave = false;
            checkBoxUseCloudSaves.Checked = playerDB.useCloudStorage;
            UpdateCloudState();
        }

        private void PlayerDB_OnUpdated(bool bCards, bool bNpcs, bool bDecks)
        {
            Logger.WriteLine("Applying cloud save (cards:" + bCards + ", npcs:" + bNpcs + ", decks:" + bDecks + ")");

            if (bCards)
            {
                updateOwnedCards(null);
            }

            if (bNpcs)
            {
                InitializeNpcUI();
            }

            if (bDecks)
            {
                PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
                if (playerDB.lastDeck.ContainsKey(currentNpc))
                {
                    TriadDeck updatedDeck = PlayerSettingsDB.Get().lastDeck[currentNpc];
                    if (!updatedDeck.Equals(playerDeck) && updatedDeck.knownCards.Count == 5)
                    {
                        TriadCard[] cardsCopy = updatedDeck.knownCards.ToArray();
                        playerDeck = new TriadDeck(cardsCopy);
                        deckCtrlSetup.SetDeck(playerDeck);
                    }

                    updateGameUIAfterDeckChange();
                }
            }

            if (cloudStorage.GetState() == GoogleDriveService.EState.NoErrors && (bCards || bNpcs || bDecks))
            {
                labelCloudState.Text = "Cloud save applied";
            }
        }

        private void checkBoxUseCloudSaves_CheckedChanged(object sender, EventArgs e)
        {
            GoogleOAuth2.KillPendingAuthorization();
            UpdateCloudState();

            PlayerSettingsDB.Get().useCloudStorage = checkBoxUseCloudSaves.Checked;

            timerCloudSave.Enabled = checkBoxUseCloudSaves.Checked;
            if (checkBoxUseCloudSaves.Checked && cloudStorage != null && cloudStorage.GetState() == GoogleDriveService.EState.NotInitialized)
            {
                buttonCloudAuth_Click(null, null);
            }
        }

        private async void buttonCloudAuth_Click(object sender, EventArgs e)
        {
            if (cloudStorage != null && !bLoadedCloudSave)
            {
                UpdateCloudState(GoogleDriveService.EState.AuthInProgress);

                try
                {
                    await cloudStorage.InitFileList();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Exception: " + ex);
                }

                UpdateCloudState();
                PlayerSettingsDB.Get().cloudToken = cloudStorage.GetAuthToken().refreshToken;

                bool bShouldSaveSettings = await LoadCloudSettings();
                bLoadedCloudSave = true;

                if (bShouldSaveSettings)
                {
                    SaveCloudSettings();
                }
            }
        }

        private void panelCloud_Click(object sender, EventArgs e)
        {
            checkBoxUseCloudSaves.Checked = !checkBoxUseCloudSaves.Checked;
        }

        private void panelCloud_Paint(object sender, PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics, panelCloud.ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        private void UpdateCloudState(GoogleDriveService.EState forcedState = GoogleDriveService.EState.NoErrors)
        {
            bool bEnabledAuthButton = false;

            if (checkBoxUseCloudSaves.Checked)
            {
                if (cloudStorage != null)
                {
                    GoogleDriveService.EState showState = (forcedState != GoogleDriveService.EState.NoErrors) ? forcedState : cloudStorage.GetState();
                    switch (showState)
                    {
                        case GoogleDriveService.EState.NoErrors: labelCloudState.Text = "active"; break;
                        case GoogleDriveService.EState.ApiFailure: labelCloudState.Text = "API call failed"; break;
                        case GoogleDriveService.EState.NotAuthorized: labelCloudState.Text = "Auth required"; bEnabledAuthButton = true; break;
                        case GoogleDriveService.EState.AuthInProgress: labelCloudState.Text = "authorizing..."; break;
                        case GoogleDriveService.EState.NotInitialized: labelCloudState.Text = "scanning..."; break;
                        default: labelCloudState.Text = ""; break;
                    }
                }
                else
                {
                    labelCloudState.Text = "database failure";
                }
            }
            else
            {
                labelCloudState.Text = "disabled, local only";
            }

            buttonCloudAuth.Visible = bEnabledAuthButton;
        }

        private async Task<bool> LoadCloudSettings()
        {
            string fileContent = null;
            try
            {
                fileContent = await cloudStorage.DownloadTextFile("FFTriadBuddy-settings.json");

                Logger.WriteLine("Loaded cloud save, API response: " + cloudStorage.GetLastApiResponse());
                if (cloudStorage.GetState() == GoogleDriveService.EState.NoErrors)
                {
                    labelCloudState.Text = "synchronized";
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Exception: " + ex);
            }

            bool bShouldSaveSettings = true;
            if (!string.IsNullOrEmpty(fileContent))
            {
                bShouldSaveSettings = PlayerSettingsDB.Get().MergeWithContent(fileContent);
            }

            return bShouldSaveSettings;
        }

        private async void SaveCloudSettings()
        {
            string fileContent = PlayerSettingsDB.Get().SaveToString();
            if (!string.IsNullOrEmpty(fileContent))
            {
                try
                {
                    await cloudStorage.UploadTextFile("FFTriadBuddy-settings.json", fileContent);

                    Logger.WriteLine("Created cloud save, API response: " + cloudStorage.GetLastApiResponse());
                    if (cloudStorage.GetState() == GoogleDriveService.EState.NoErrors)
                    {
                        labelCloudState.Text = "stored";
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Exception: " + ex);
                }
            }
        }

        private void timerCloudSave_Tick(object sender, EventArgs e)
        {
            if (bLoadedCloudSave && cloudStorage != null)
            {
                if (PlayerSettingsDB.Get().isDirty)
                {
                    SaveCloudSettings();
                }
                else
                {
                    labelCloudState.Text = "synchronized";
                }
            }
        }

        #endregion

    }
}
