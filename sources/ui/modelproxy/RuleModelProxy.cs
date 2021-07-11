using System;

namespace FFTriadBuddy.UI
{
    // viewmodel wrapper for model class: modifier / rule
    public class RuleModelProxy : LocalizedViewModel, IComparable, IImageHashMatch
    {
        public readonly TriadGameModifier modOb;

        public string NameLocalized => modOb.GetLocalizedName();

        public RuleModelProxy(TriadGameModifier triadMod)
        {
            modOb = triadMod;
        }

        public int CompareTo(object obj)
        {
            var otherRule = obj as RuleModelProxy;
            return (otherRule != null) ? NameLocalized.CompareTo(otherRule.NameLocalized) : 0;
        }

        public object GetMatchOwner()
        {
            return modOb;
        }
    }
}
