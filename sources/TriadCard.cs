using System;

namespace FFTriadBuddy
{
    public enum ETriadCardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum ETriadCardType
    {
        None,
        Beastman,
        Primal,
        Scion,
        Garlean,
    }

    public enum ETriadCardOwner
    {
        Unknown,
        Blue,
        Red,
    }

    public enum ETriadGameSide
    {
        Up,
        Left,
        Down,
        Right,
    }

    public class TriadCard : IEquatable<TriadCard>
    {
        public int Id;
        public string Name;
        public string IconPath;
        public ETriadCardRarity Rarity;
        public ETriadCardType Type;
        public int[] Sides;
        public int SameNumberId;
        public int SortKey;

        public TriadCard()
        {
            Id = -1;
            Sides = new int[4] { 0, 0, 0, 0 };
            SameNumberId = -1;
            SortKey = 0;
        }

        public TriadCard(int id, string name, string iconPath, ETriadCardRarity rarity, ETriadCardType type, int numUp, int numDown, int numLeft, int numRight, int sortKey)
        {
            Id = id;
            Name = name;
            IconPath = iconPath;
            Rarity = rarity;
            Type = type;
            Sides = new int[4] { numUp, numLeft, numDown, numRight };
            SameNumberId = -1;
            SortKey = sortKey;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TriadCard);
        }

        public bool Equals(TriadCard other)
        {
            return (other != null) && (Id == other.Id);
        }

        public override int GetHashCode()
        {
            return 2108858624 + Id.GetHashCode();
        }

        public bool IsValid()
        {
            return (Id >= 0) &&
                (Sides[0] >= 1) && (Sides[0] <= 10) &&
                (Sides[1] >= 1) && (Sides[1] <= 10) &&
                (Sides[2] >= 1) && (Sides[2] <= 10) &&
                (Sides[3] >= 1) && (Sides[3] <= 10);
        }

        public string ToShortString()
        {
            return "[" + Id + ":" + Name + "]";
        }

        public override string ToString()
        {
            string desc = "[" + Id + "] " + Name + " ";
            for (int Idx = 0; Idx <= (int)Rarity; Idx++)
            {
                desc += "*";
            }

            desc += " [" + Sides[0] + ", " + Sides[1] + ", " + Sides[2] + ", " + Sides[3] + "]";
            desc += ((Type != ETriadCardType.None) ? " [" + Type + "]" : "");

            return desc;
        }
    }

    public class TriadCardInstance
    {
        public readonly TriadCard card;
        public ETriadCardOwner owner;
        public int scoreModifier;

        public TriadCardInstance(TriadCard card, ETriadCardOwner owner)
        {
            this.card = card;
            this.owner = owner;
            scoreModifier = 0;
        }

        public TriadCardInstance(TriadCardInstance copyFrom)
        {
            card = copyFrom.card;
            owner = copyFrom.owner;
            scoreModifier = copyFrom.scoreModifier;
        }

        public override string ToString()
        {
            return owner + " " + card +
                ((scoreModifier > 0) ? (" +" + scoreModifier) :
                 (scoreModifier < 0) ? (" -" + scoreModifier) :
                 "");
        }

        public int GetRawNumber(ETriadGameSide side)
        {
            return card.Sides[(int)side];
        }

        public int GetNumber(ETriadGameSide side)
        {
            return Math.Min(Math.Max(GetRawNumber(side) + scoreModifier, 1), 10);
        }

        public int GetOppositeNumber(ETriadGameSide side)
        {
            return GetNumber((ETriadGameSide)(((int)side + 2) % 4));
        }
    }
}
