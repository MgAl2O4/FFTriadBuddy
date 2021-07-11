using System;

namespace FFTriadBuddy.UI
{
    // viewmodel wrapper for model class: npc
    public class NpcModelProxy : LocalizedViewModel, IComparable
    {
        public readonly TriadNpc npcOb;

        public string NameLocalized => npcOb.Name.GetLocalized();
        public string LocationLocalized => npcOb.GetLocationDesc();
        public int DescPower => npcOb.Deck.GetPower();

        private bool isCompleted = false;
        public bool IsCompleted
        {
            get { return isCompleted; }
            set { isCompleted = value; OnPropertyChanged(); }
        }

        private string descReward;
        public string DescReward
        {
            get { return descReward; }
            set { descReward = value; OnPropertyChanged(); }
        }

        private string descRules;
        public string DescRules
        {
            get { return descRules; }
            set { descRules = value; OnPropertyChanged(); }
        }

        private string descCompleted;
        public string DescCompleted
        {
            get { return descCompleted; }
            set { descCompleted = value; OnPropertyChanged(); }
        }

        public int CompareTo(object obj)
        {
            var otherNpc = obj as NpcModelProxy;
            return (otherNpc != null) ? NameLocalized.CompareTo(otherNpc.NameLocalized) : 0;
        }

        public NpcModelProxy(TriadNpc triadNpc)
        {
            npcOb = triadNpc;

            UpdateCachedText();
        }

        public void UpdateCachedText(bool sendNotifies = true)
        {
            var newDescRules = "";
            foreach (var rule in npcOb.Rules)
            {
                if (newDescRules.Length > 0) { newDescRules += ", "; }
                newDescRules += rule.GetLocalizedName();
            }

            if (newDescRules.Length == 0)
            {
                newDescRules = loc.strings.MainForm_Dynamic_RuleListEmpty;
            }

            PlayerSettingsDB settingsDB = PlayerSettingsDB.Get();
            var newDescRewards = "";
            foreach (var reward in npcOb.Rewards)
            {
                if (!settingsDB.ownedCards.Contains(reward))
                {
                    if (newDescRewards.Length > 0) { newDescRewards += ", "; }
                    newDescRewards += reward.Name.GetLocalized();
                }
            }

            descRules = newDescRules;
            descReward = newDescRewards;
            descCompleted = IsCompleted ? loc.strings.MainForm_Dynamic_NpcCompletedColumn : "";

            if (sendNotifies)
            {
                DescRules = descRules;
                DescReward = descReward;
                DescCompleted = descCompleted;
            }
        }

        public override void RefreshLocalization()
        {
            OnPropertyChanged("NameLocalized");
            OnPropertyChanged("LocationLocalized");
            UpdateCachedText();
        }
    }
}
