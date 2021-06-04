#if DEBUG

using FFTriadBuddy.Datamine;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FFTriadBuddy
{
    class DataConverter
    {
        public void Run()
        {
            GameDataLists gameDataLists = new GameDataLists();
            gameDataLists.Load(@"..\..\..\datasource\export\exd-all\");

            bool result = gameDataLists.Link();
            if (result)
            {
                var mapRuleByCodeName = BuildRuleNameMap();
                var mapCardTypeByCodeName = BuildCardTypes();

                result = result && ExportRuleNames(gameDataLists.rules, mapRuleByCodeName);
                result = result && ExportCardTypes(gameDataLists.cardTypes, mapCardTypeByCodeName);
                result = result && UpdateCards(gameDataLists, mapCardTypeByCodeName);
                result = result && UpdateNpcs(gameDataLists, mapRuleByCodeName);
                result = result && UpdateTournaments(gameDataLists, mapRuleByCodeName);
            }

            if (result)
            {
                LocalizationDB.Get().Save();
                TriadCardDB.Get().Save();
                TriadNpcDB.Get().Save();
                TriadTournamentDB.Get().Save();
            }

            Logger.WriteLine(result ? "Done." : "Aborted");
        }

        private Dictionary<string, TriadGameModifier> BuildRuleNameMap()
        {
            var ruleMap = new Dictionary<string, TriadGameModifier>();
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)) && type != typeof(TriadGameModifierNone))
                {
                    TriadGameModifier modInst = (TriadGameModifier)Activator.CreateInstance(type);
                    ruleMap.Add(modInst.GetCodeName(), modInst);
                }
            }

            return ruleMap;
        }

        private bool ExportRuleNames(List<GameDataRule> gameDataRules, Dictionary<string, TriadGameModifier> ruleMap)
        {
            var ruleCodeNames = new List<string>();
            foreach (var ruleInfo in gameDataRules)
            {
                ruleCodeNames.Add(ruleInfo.Name.GetCodeName());
            }

            LocalizationDB locDB = LocalizationDB.Get();
            {
                TriadGameModifierNone ruleNone = new TriadGameModifierNone();
                locDB.LocRuleNames[ruleNone.GetLocalizationId()].Text[LocalizationDB.CodeLanguageIdx] = "";
            }

            foreach (var kvp in ruleMap)
            {
                var matchGameData = gameDataRules.Find(x => (x.Name.GetCodeName() == kvp.Key));
                if (matchGameData == null)
                {
                    Logger.WriteLine("FAILED rule export, no match for: {0}", kvp.Key);
                    return false;
                }

                locDB.LocRuleNames[kvp.Value.GetLocalizationId()].Text = matchGameData.Name.Text;
                ruleCodeNames.Remove(kvp.Key);
            }

            if (ruleCodeNames.Count > 0)
            {
                Logger.WriteLine("FAILED rule export, not assigned: {0}", string.Join(", ", ruleCodeNames));
                return false;
            }

            return true;
        }

        private Dictionary<string, ETriadCardType> BuildCardTypes()
        {
            var typeMap = new Dictionary<string, ETriadCardType>();

            LocalizationDB locDB = LocalizationDB.Get();
            for (int idx = 0; idx < locDB.LocCardTypes.Count; idx++)
            {
                typeMap.Add(locDB.LocCardTypes[idx].GetCodeName(), (ETriadCardType)idx);
            }

            return typeMap;
        }

        private bool ExportCardTypes(List<GameDataCardType> gameDataCardTypes, Dictionary<string, ETriadCardType> typeMap)
        {
            var typeCodeNames = new List<string>();
            foreach (var typeInfo in gameDataCardTypes)
            {
                typeCodeNames.Add(typeInfo.Type.GetCodeName());
            }

            LocalizationDB locDB = LocalizationDB.Get();
            foreach (var kvp in typeMap)
            {
                if (kvp.Value == ETriadCardType.None)
                {
                    continue;
                }

                var matchGameData = gameDataCardTypes.Find(x => (x.Type.GetCodeName() == kvp.Key));
                if (matchGameData == null)
                {
                    Logger.WriteLine("FAILED card type export, no match for: {0}", kvp.Key);
                    return false;
                }

                locDB.mapCardTypes[kvp.Value].Text = matchGameData.Type.Text;
                typeCodeNames.Remove(kvp.Key);
            }

            if (typeCodeNames.Count > 0)
            {
                Logger.WriteLine("FAILED card type export, not assigned: {0}", string.Join(", ", typeCodeNames));
                return false;
            }

            return true;
        }

        private bool UpdateCards(GameDataLists gameDataLists, Dictionary<string, ETriadCardType> mapCardTypes)
        {
            Logger.WriteLine("Updating card list...");

            TriadCardDB cardDB = TriadCardDB.Get();
            foreach (var cardData in gameDataLists.cards)
            {
                TriadCard cardOb = (cardData.Id < cardDB.cards.Count) ? cardDB.cards[cardData.Id] : null;
                if (cardOb != null)
                {
                    // ensure side numbers are the same
                    if (cardOb.Sides[0] != cardData.sideTop ||
                        cardOb.Sides[1] != cardData.sideLeft ||
                        cardOb.Sides[2] != cardData.sideBottom ||
                        cardOb.Sides[3] != cardData.sideRight)
                    {
                        Logger.WriteLine("FAILED card update, id:{0} name:{1} is not matching side numbers!", cardData.Id, cardData.LinkedName.Name.GetCodeName());
                        return false;
                    }
                }
                else
                {
                    while (cardDB.cards.Count <= cardData.Id)
                    {
                        cardDB.cards.Add(null);
                    }

                    int iconId = 82500 + cardData.Id;
                    string iconPath = iconId.ToString("000000") + ".png";

                    cardOb = new TriadCard(cardData.Id,
                        iconPath,
                        (ETriadCardRarity)(cardData.rarityIdx - 1),
                        mapCardTypes[cardData.LinkedType == null ? "" : cardData.LinkedType.Type.GetCodeName()],
                        cardData.sideTop,
                        cardData.sideBottom,
                        cardData.sideLeft,
                        cardData.sideRight,
                        cardData.sortOrder,
                        cardData.uiGroup);

                    Logger.WriteLine(">> adding new card: " + cardOb.ToString());
                    cardDB.cards[cardData.Id] = cardOb;
                }

                cardOb.Name.Text = cardData.LinkedName.Name.Text;
            }

            for (int idx = 0; idx < cardDB.cards.Count; idx++)
            {
                TriadCard cardOb = cardDB.cards[idx];
                if (cardOb != null)
                {
                    var matchingCardData = gameDataLists.cards.Find(x => (x.Id == cardOb.Id));
                    if (matchingCardData == null)
                    {
                        Logger.WriteLine(">> removing card: " + cardOb.ToString());
                        cardDB.cards[idx] = null;
                        continue;
                    }

                    if (cardOb.Id != idx)
                    {
                        Logger.WriteLine("FAILED card update, index mismatch for card[{0}].Id:{1}, Name:{2}", idx, cardOb.Id, cardOb.Name.GetCodeName());
                        return false;
                    }
                }
            }

            return true;
        }

        private bool UpdateNpcs(GameDataLists gameDataLists, Dictionary<string, TriadGameModifier> mapRuleNames)
        {
            Logger.WriteLine("Updating npc list...");

            TriadCardDB cardDB = TriadCardDB.Get();
            TriadNpcDB npcDB = TriadNpcDB.Get();
            var validDeckIds = new List<string>();

            foreach (var npcData in gameDataLists.npcs)
            {
                if (npcData.LinkedNpcId == null)
                {
                    continue;
                }

                if (npcData.LinkedNpcId.LinkedName == null || npcData.LinkedNpcId.LinkedLocation == null)
                {
                    continue;
                }

                TriadDeck npcDataDeck = new TriadDeck();
                foreach (var cardData in npcData.LinkedCardsFixed)
                {
                    npcDataDeck.knownCards.Add(cardDB.cards[cardData.Id]);
                }
                foreach (var cardData in npcData.LinkedCardsVariable)
                {
                    npcDataDeck.unknownCardPool.Add(cardDB.cards[cardData.Id]);
                }
                npcDataDeck.UpdateDeckId();
                validDeckIds.Add(npcDataDeck.deckId);

                // mistakes were made...
                TriadNpc npcOb = npcDB.FindByDeckId(npcDataDeck.deckId);
                int npcId = (npcOb == null) ? npcDB.npcs.Count : npcOb.Id;

                if (npcOb != null)
                {
                    // ensure decks are the same
                    if (!npcOb.Deck.Equals(npcDataDeck))
                    {
                        Logger.WriteLine("FAILED npc update, id:{0} name:{1} is not matching cards!", npcId, npcData.LinkedNpcId.LinkedName.Name.GetCodeName());
                        return false;
                    }
                }
                else
                {
                    while (npcDB.npcs.Count <= npcId)
                    {
                        npcDB.npcs.Add(null);
                    }

                    var listMods = new List<TriadGameModifier>();
                    foreach (var ruleData in npcData.LinkedRules)
                    {
                        listMods.Add(mapRuleNames[ruleData.Name.GetCodeName()]);
                    }

                    var listRewards = new List<TriadCard>();
                    foreach (var rewardData in npcData.LinkedRewards)
                    {
                        listRewards.Add(cardDB.cards[rewardData.LinkedCard.Id]);
                    }

                    npcOb = new TriadNpc(npcId, listMods, listRewards, npcDataDeck);

                    Logger.WriteLine(">> adding new npc: " + npcOb.ToString());
                    npcDB.npcs[npcId] = npcOb;
                }

                var linkedLoc = npcData.LinkedNpcId.LinkedLocation;
                linkedLoc.LinkedMap.GetCoords(linkedLoc.ScaledPosX, linkedLoc.ScaledPosZ, out npcOb.LocationX, out npcOb.LocationY);

                npcOb.Name.Text = npcData.LinkedNpcId.LinkedName.Name.Text;
                npcOb.LocationMap.Text = npcData.LinkedNpcId.LinkedLocation.LinkedMap.LinkedPlace.Name.Text;
            }

            for (int idx = 0; idx < npcDB.npcs.Count; idx++)
            {
                TriadNpc npcOb = npcDB.npcs[idx];
                if (npcOb != null)
                {
                    if (!validDeckIds.Contains(npcOb.Deck.deckId))
                    {
                        Logger.WriteLine(">> removing npc: " + npcOb.ToString() + ", deck:" + npcOb.Deck.ToString());
                        npcDB.npcs[idx] = null;
                        continue;
                    }

                    if (npcOb.Id != idx)
                    {
                        Logger.WriteLine("FAILED npc update, index mismatch for npc[{0}].Id:{1}, Name:{2}", idx, npcOb.Id, npcOb.Name.GetCodeName());
                        return false;
                    }
                }
            }

            return true;
        }

        private bool UpdateTournaments(GameDataLists gameDataLists, Dictionary<string, TriadGameModifier> mapRuleNames)
        {
            Logger.WriteLine("Updating tournament list...");

            // TODO: not sure how to find it in .csv data,
            // hardcode entries + rules for now
            // alert new new unique entry appears

            var uniqueTournamentNames = new List<string>();
            var uniqueTournamentLocNames = new List<LocString>();
            foreach (var tourData in gameDataLists.tournamentNames)
            {
                var codeName = tourData.Name.GetCodeName();
                if (!string.IsNullOrEmpty(codeName))
                {
                    if (!uniqueTournamentNames.Contains(codeName))
                    {
                        uniqueTournamentNames.Add(codeName);
                        uniqueTournamentLocNames.Add(tourData.Name);
                    }
                }
            }

            string[] hardcodedNames = { "the Manderville Tournament of Champions", "the Spinner's Pull", "the Durai Memorial", "the Rowena Cup Classic" };
            string[] hardcodedRules =
            {
                "All Open", "Plus",
                "Three Open", "Swap",
                "Order", "Same",
                "Roulette", "Roulette",
            };

            if (uniqueTournamentNames.Count != hardcodedNames.Length)
            {
                Logger.WriteLine("FAILED tournament update, hardcoded list diff! [{0}]", string.Join(", ", uniqueTournamentNames));
                return false;
            }

            TriadTournamentDB tournamentDB = TriadTournamentDB.Get();
            for (int idx = 0; idx < uniqueTournamentNames.Count; idx++)
            {
                if (uniqueTournamentNames[idx] != hardcodedNames[idx])
                {
                    Logger.WriteLine("FAILED tournament update, id:{0} mismatch!", idx);
                    return false;
                }

                TriadTournament tourOb = (idx < tournamentDB.tournaments.Count) ? tournamentDB.tournaments[idx] : null;
                if (tourOb == null)
                {
                    while (tournamentDB.tournaments.Count <= idx)
                    {
                        tournamentDB.tournaments.Add(new TriadTournament(idx, new List<TriadGameModifier>()));
                    }

                    tourOb = tournamentDB.tournaments[idx];
                }

                tourOb.Name.Text = uniqueTournamentLocNames[idx].Text;

                tourOb.Rules.Clear();
                int ruleStartIdx = idx * 2;
                for (int ruleIdx = 0; ruleIdx < 2; ruleIdx++)
                {
                    tourOb.Rules.Add(mapRuleNames[hardcodedRules[ruleIdx + ruleStartIdx]]);
                }
            }

            return true;
        }
    }
}

#endif // DEBUG
