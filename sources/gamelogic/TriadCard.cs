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
        public LocString Name;
        public ETriadCardRarity Rarity;
        public ETriadCardType Type;
        public int[] Sides;
        public int SameNumberId;
        public int SortOrder;
        public int Group;
        public float OptimizerScore;

        public int SmallIconId => 88000 + Id;
        public int BigIconId => 87000 + Id;

        public TriadCard()
        {
            Id = -1;
            Sides = new int[4] { 0, 0, 0, 0 };
            SameNumberId = -1;
            SortOrder = 0;
            Group = 0;
            OptimizerScore = 0.0f;
        }

        public TriadCard(int id, ETriadCardRarity rarity, ETriadCardType type, int numUp, int numDown, int numLeft, int numRight, int sortOrder, int group)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.CardName, id);
            Rarity = rarity;
            Type = type;
            Sides = new int[4] { numUp, numLeft, numDown, numRight };
            SameNumberId = -1;
            SortOrder = sortOrder;
            Group = group;

            if (group != 0 && SortOrder < 1000)
            {
                SortOrder += 1000;
            }

            OptimizerScore = TriadDeckOptimizer.GetCardScore(this);
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

        public string ToShortCodeString()
        {
            return "[" + Id + ":" + Name.GetCodeName() + "]";
        }

        public string ToShortLocalizedString()
        {
            return "[" + Id + ":" + Name.GetLocalized() + "]";
        }

        public string ToLocalizedString()
        {
            return string.Format("[{0}] {1} {2} [{3}, {4}, {5}, {6}]",
                Id, Name.GetLocalized(),
                new string('*', (int)Rarity + 1),
                Sides[0], Sides[1], Sides[2], Sides[3],
                (Type != ETriadCardType.None) ? " [" + LocalizationDB.Get().LocCardTypes[(int)Type] + "]" : "");
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} {2} [{3}, {4}, {5}, {6}]",
                Id, Name.GetCodeName(),
                new string('*', (int)Rarity + 1),
                Sides[0], Sides[1], Sides[2], Sides[3],
                (Type != ETriadCardType.None) ? " [" + Type + "]" : "");
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
