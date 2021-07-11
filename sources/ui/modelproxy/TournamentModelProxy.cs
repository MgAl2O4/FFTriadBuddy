using System;

namespace FFTriadBuddy.UI
{
    // viewmodel wrapper for model class: tournament
    public class TournamentModelProxy : LocalizedViewModel, IComparable
    {
        public readonly TriadTournament tournamentOb;

        public string NameLocalized => tournamentOb.Name.GetLocalized();

        private string descRules;
        public string DescRules
        {
            get { return descRules; }
            set { descRules = value; OnPropertyChanged(); }
        }

        public TournamentModelProxy(TriadTournament triadTournament)
        {
            tournamentOb = triadTournament;
            UpdateCachedText();
        }

        public int CompareTo(object obj)
        {
            var otherTournament = obj as TournamentModelProxy;
            return (otherTournament != null) ? NameLocalized.CompareTo(otherTournament.NameLocalized) : 0;
        }

        public void UpdateCachedText(bool sendNotifies = true)
        {
            var newDescRules = "";
            foreach (var rule in tournamentOb.Rules)
            {
                if (newDescRules.Length > 0) { newDescRules += ", "; }
                newDescRules += rule.GetLocalizedName();
            }

            if (newDescRules.Length == 0)
            {
                newDescRules = loc.strings.MainForm_Dynamic_RuleListEmpty;
            }

            descRules = newDescRules;
            if (sendNotifies)
            {
                DescRules = descRules;
            }
        }

        public override void RefreshLocalization()
        {
            OnPropertyChanged("NameLocalized");
            UpdateCachedText();
        }
    }
}
