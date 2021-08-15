using System;
using System.Collections.Generic;

namespace FFTriadBuddy
{
    public class TriadNpc
    {
        public int Id;
        public LocString Name;
        public List<TriadGameModifier> Rules;
        public TriadDeck Deck;

        public TriadNpc(int id, List<TriadGameModifier> rules, int[] cardsAlways, int[] cardsPool)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcName, id);
            Rules = rules;
            Deck = new TriadDeck(cardsAlways, cardsPool);
        }

        public TriadNpc(int id, List<TriadGameModifier> rules, List<TriadCard> rewards, TriadDeck deck)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcName, id);
            Rules = rules;
            Deck = deck;
        }

        public override string ToString()
        {
            return Name.GetCodeName();
        }
    }

    public class TriadNpcDB
    {
        private static TriadNpcDB instance = new TriadNpcDB();
        public List<TriadNpc> npcs = new List<TriadNpc>();

        public static TriadNpcDB Get()
        {
            return instance;
        }

        public TriadNpc Find(string Name)
        {
            return npcs.Find(x => (x != null) && x.Name.GetCodeName().Equals(Name, StringComparison.OrdinalIgnoreCase));
        }

        public TriadNpc FindByNameStart(string Name)
        {
            return npcs.Find(x => (x != null) && x.Name.GetCodeName().StartsWith(Name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
