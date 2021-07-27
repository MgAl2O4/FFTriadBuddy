using System;
using System.Windows.Threading;

namespace FFTriadBuddy.UI
{
    public class SolvableDeckViewModel : LocalizedViewModel
    {
        public CardCollectionViewModel Deck { get; set; }
        public WinChanceViewModel WinChance { get; } = new WinChanceViewModel();
        public FavDeckSolver solver;

        private DispatcherTimer timer;
        private TriadDeck cachedDeckModel;
        private bool hasModelHook = false;

        private bool isSolverRunning = false;
        public bool IsSolverRunning { get => isSolverRunning; set => PropertySetAndNotify(value, ref isSolverRunning); }
        public int SolverTaskId => solver.calcId;

        public int Progress => solver.progress;
        public string DescProbability => isSolverRunning ? "..." : WinChance.DescProbability;

        private static int NextSolverId = 0;

        public SolvableDeckViewModel()
        {
            solver = new FavDeckSolver() { contextId = NextSolverId };
            solver.OnSolved += Solver_OnSolved;
            NextSolverId++;
        }

        private void Solver_OnSolved(int id, TriadDeck deck, TriadGameResultChance chance)
        {
            IsSolverRunning = false;
            timer?.Stop();

            if (cachedDeckModel.Equals(deck))
            {
                WinChance.SetValue(chance);
                OnPropertyChanged("DescProbability");
            }
        }

        public void InitializeFor(TriadGameModel gameModel)
        {
            if (!hasModelHook)
            {
                gameModel.OnSetupChanged += (model) => RefreshSolver(model, cachedDeckModel);
                hasModelHook = true;
            }
        }

        public void RefreshSolver(TriadGameModel gameModel, TriadDeck deck)
        {
            cachedDeckModel = deck;
            InitializeFor(gameModel);

            int lastCalcId = solver.calcId;
            solver.Update(gameModel.Session, gameModel.Npc);
            solver.SetDeck(deck);

            if (lastCalcId != solver.calcId)
            {
                IsSolverRunning = true;
                timer?.Start();

                OnPropertyChanged("DescProbability");
                OnPropertyChanged("Progress");
            }
        }

        public override void RefreshLocalization()
        {
            WinChance.RefreshLocalization();

            base.RefreshLocalization();
        }

        public void EnableTrackingProgress()
        {
            timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(0.1) };
            timer.Tick += ProgressUpdate_Tick;
        }

        private void ProgressUpdate_Tick(object sender, EventArgs e)
        {
            OnPropertyChanged("Progress");
        }
    }
}
