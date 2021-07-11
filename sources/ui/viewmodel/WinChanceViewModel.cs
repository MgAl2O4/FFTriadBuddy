namespace FFTriadBuddy.UI
{
    public class WinChanceViewModel : LocalizedViewModel
    {
        public bool isInvalid = true;
        public TriadGameResultChance chance;

        public bool IsDraw => chance.expectedResult == ETriadGameState.BlueDraw;
        public bool IsWin => chance.expectedResult == ETriadGameState.BlueWins;
        public float Probability => isInvalid ? 0.0f :
            (chance.expectedResult == ETriadGameState.BlueWins) ? chance.winChance :
            chance.drawChance;

        public string DescProbability => isInvalid ? "0%" :
            (chance.expectedResult == ETriadGameState.BlueWins) ? chance.winChance.ToString("P0") :
            string.Format("{0:P0} {1}", chance.drawChance, loc.strings.MainForm_Dynamic_Simulate_ChanceIsDraw);

        public void SetInvalid()
        {
            isInvalid = true;
            NotifyProperties();
        }

        public void SetValue(TriadGameResultChance value)
        {
            chance = value;
            isInvalid = false;
            NotifyProperties();
        }

        private void NotifyProperties()
        {
            OnPropertyChanged("IsDraw");
            OnPropertyChanged("IsWin");
            OnPropertyChanged("Probability");
            OnPropertyChanged("DescProbability");
        }

        public void CopyFrom(WinChanceViewModel other)
        {
            if (other.isInvalid)
            {
                SetInvalid();
            }
            else
            {
                SetValue(other.chance);
            }
        }
    }
}
