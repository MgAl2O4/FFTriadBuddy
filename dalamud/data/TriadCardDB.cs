using System;
using System.Collections.Generic;

namespace FFTriadBuddy
{
    public class TriadCardDB
    {
        private static TriadCardDB instance = new TriadCardDB();

        public List<TriadCard> cards = new List<TriadCard>();
        public Dictionary<int, List<TriadCard>> sameNumberMap = new Dictionary<int, List<TriadCard>>();
        public TriadCard hiddenCard;

        public TriadCardDB()
        {
            hiddenCard = new TriadCard(0, null, ETriadCardRarity.Common, ETriadCardType.None, 0, 0, 0, 0, 0, 0);
            hiddenCard.Name.Text = "(hidden)"; // debug only, ignore localization
        }

        public static TriadCardDB Get()
        {
            return instance;
        }

        public TriadCard Find(string Name)
        {
            return cards.Find(x => (x != null) && x.Name.GetCodeName().Equals(Name, StringComparison.OrdinalIgnoreCase));
        }

        public TriadCard Find(int numUp, int numLeft, int numDown, int numRight)
        {
            // side number may be ambiguous! returns first match
            return cards.Find(x =>
                (x != null) &&
                (x.Sides[(int)ETriadGameSide.Up] == numUp) &&
                (x.Sides[(int)ETriadGameSide.Down] == numDown) &&
                (x.Sides[(int)ETriadGameSide.Left] == numLeft) &&
                (x.Sides[(int)ETriadGameSide.Right] == numRight));
        }

        public TriadCard FindByTexture(string texPath)
        {
            // map image ids: 082100+ directly to card id: 0+
            // path example: ui/icon/082000/082145.tex

            if (texPath.EndsWith(".tex") && texPath.Length > 11 &&
                texPath[texPath.Length - 11] == '/' &&
                texPath[texPath.Length - 10] == '0' &&
                texPath[texPath.Length - 9] == '8' &&
                texPath[texPath.Length - 8] == '2')
            {
                string idStr = texPath.Substring(texPath.Length - 7, 3);
                if (int.TryParse(idStr, out int cardId))
                {
                    cardId -= 100;
                    if (cardId >= 0 && cardId < cards.Count)
                    {
                        return cards[cardId];
                    }
                }
            }

            return null;
        }

        public void ProcessSameSideLists()
        {
            sameNumberMap.Clear();
            int sameNumberId = 0;
            for (int Idx1 = 0; Idx1 < cards.Count; Idx1++)
            {
                TriadCard card1 = cards[Idx1];
                if (card1 != null && card1.SameNumberId < 0)
                {
                    bool bHasSameNumberCards = false;
                    for (int Idx2 = (Idx1 + 1); Idx2 < cards.Count; Idx2++)
                    {
                        TriadCard card2 = cards[Idx2];
                        if (card2 != null && card2.SameNumberId < 0)
                        {
                            bool bHasSameNumbers =
                                (card1.Sides[0] == card2.Sides[0]) &&
                                (card1.Sides[1] == card2.Sides[1]) &&
                                (card1.Sides[2] == card2.Sides[2]) &&
                                (card1.Sides[3] == card2.Sides[3]);

                            bHasSameNumberCards = bHasSameNumberCards || bHasSameNumbers;
                            if (bHasSameNumbers)
                            {
                                if (!sameNumberMap.ContainsKey(sameNumberId))
                                {
                                    sameNumberMap.Add(sameNumberId, new List<TriadCard>());
                                    sameNumberMap[sameNumberId].Add(card1);
                                    card1.SameNumberId = sameNumberId;
                                }

                                sameNumberMap[sameNumberId].Add(card2);
                                card2.SameNumberId = sameNumberId;
                            }
                        }
                    }

                    if (bHasSameNumberCards)
                    {
                        sameNumberId++;
                    }
                }
            }
        }
    }
}
