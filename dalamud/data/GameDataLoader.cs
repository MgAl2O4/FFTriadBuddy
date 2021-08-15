using Dalamud.Plugin;
using FFTriadBuddy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TriadBuddyPlugin
{
    public class GameDataLoader
    {
        public bool IsDataReady { get; private set; } = false;

        public void StartAsyncWork(DalamudPluginInterface pluginInterface)
        {
            Task.Run(() =>
            {
                bool result = true;
                result = result && ParseRules(pluginInterface);
                result = result && ParseCards(pluginInterface);
                result = result && ParseNpcs(pluginInterface);

                var cardDB = TriadCardDB.Get();
                var npcDB = TriadNpcDB.Get();

                if (result)
                {
                    PluginLog.Log($"Loaded game data for cards:{cardDB.cards.Count}, npcs:{npcDB.npcs.Count}");
                    IsDataReady = true;
                }
                else
                {
                    // welp. can't do anything at this point, clear all DBs
                    // UI scraping will fail when data is missing there

                    cardDB.cards.Clear();
                    npcDB.npcs.Clear();
                }
            });
        }

        private bool ParseRules(DalamudPluginInterface pluginInterface)
        {
            // update rule names to match current client language
            // modifier locIds are already matching order in game data sheet

            var modDB = TriadGameModifierDB.Get();
            var locDB = LocalizationDB.Get();

            var rulesSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriadRule>();
            if (rulesSheet == null || rulesSheet.RowCount != modDB.mods.Count)
            {
                PluginLog.Fatal($"Failed to parse rules (got:{rulesSheet?.RowCount ?? 0}, expected:{modDB.mods.Count})");
                return false;
            }

            for (int idx = 0; idx < modDB.mods.Count; idx++)
            {
                var mod = modDB.mods[idx];
                var locStr = locDB.LocRuleNames[mod.GetLocalizationId()];

                locStr.Text = rulesSheet.GetRow((uint)idx).Name;
            }

            return true;
        }

        private bool ParseCards(DalamudPluginInterface pluginInterface)
        {
            var cardDB = TriadCardDB.Get();

            var cardDataSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriadCardResident>();
            var cardNameSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriadCard>();

            if (cardDataSheet != null && cardNameSheet != null && cardDataSheet.RowCount == cardNameSheet.RowCount)
            {
                // meh, hardcode mappings, if SE adds new type or rarity more stuff will break anyway
                ETriadCardType[] cardTypeMap = { ETriadCardType.None, ETriadCardType.Primal, ETriadCardType.Scion, ETriadCardType.Beastman, ETriadCardType.Garlean };
                ETriadCardRarity[] cardRarityMap = { ETriadCardRarity.Common, ETriadCardRarity.Common, ETriadCardRarity.Uncommon, ETriadCardRarity.Rare, ETriadCardRarity.Epic, ETriadCardRarity.Legendary };

                var cardTypesSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriadCardType>();
                var cardRaritySheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriadCardRarity>();
                if (cardTypesSheet == null || cardTypesSheet.RowCount != cardTypeMap.Length)
                {
                    PluginLog.Fatal($"Failed to parse card types (got:{cardTypesSheet?.RowCount ?? 0}, expected:{cardTypeMap.Length})");
                    return false;
                }
                if (cardRaritySheet == null || cardRaritySheet.RowCount != cardRarityMap.Length)
                {
                    PluginLog.Fatal($"Failed to parse card rarities (got:{cardRaritySheet?.RowCount ?? 0}, expected:{cardRarityMap.Length})");
                    return false;
                }

                for (uint idx = 0; idx < cardDataSheet.RowCount; idx++)
                {
                    var rowData = cardDataSheet.GetRow(idx);
                    var rowName = cardNameSheet.GetRow(idx);

                    if (rowData.Top > 0)
                    {
                        var rowTypeId = rowData.TripleTriadCardType.Row;
                        var rowRarityId = rowData.TripleTriadCardRarity.Row;
                        var cardType = (rowTypeId < cardTypeMap.Length) ? cardTypeMap[rowTypeId] : ETriadCardType.None;
                        var cardRarity = (rowRarityId < cardRarityMap.Length) ? cardRarityMap[rowRarityId] : ETriadCardRarity.Common;

                        var cardOb = new TriadCard((int)idx, null, cardRarity, cardType, rowData.Top, rowData.Bottom, rowData.Left, rowData.Right, rowData.SortKey, rowData.UIPriority);
                        cardOb.Name.Text = rowName.Name.RawString;

                        cardDB.cards.Add(cardOb);
                    }
                }
            }
            else
            {
                PluginLog.Fatal($"Failed to parse card data (D:{cardDataSheet?.RowCount ?? 0}, N:{cardNameSheet?.RowCount ?? 0})");
                return false;
            }

            cardDB.ProcessSameSideLists();
            return true;
        }

        private bool ParseNpcs(DalamudPluginInterface pluginInterface)
        {
            var npcDB = TriadNpcDB.Get();

            // cards & rules can be mapped directly from their respective DBs
            var cardDB = TriadCardDB.Get();
            var modDB = TriadGameModifierDB.Get();

            // name is a bit more annoying to get
            var listTriadIds = new List<uint>();

            var npcDataSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TripleTriad>();
            if (npcDataSheet != null)
            {
                // rowIds are not going from 0 here!
                foreach (var rowData in npcDataSheet)
                {
                    listTriadIds.Add(rowData.RowId);
                }
            }

            listTriadIds.Remove(0);
            if (listTriadIds.Count == 0)
            {
                PluginLog.Fatal("Failed to parse npc data (missing ids)");
                return false;
            }

            var mapTriadNpcNames = new Dictionary<uint, string>();
            var sheetNpcNames = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ENpcResident>();
            var sheetENpcBase = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ENpcBase>();
            if (sheetNpcNames != null && sheetENpcBase != null)
            {
                foreach (var rowData in sheetENpcBase)
                {
                    var triadId = Array.Find(rowData.ENpcData, id => listTriadIds.Contains(id));
                    if (triadId != 0 && !mapTriadNpcNames.ContainsKey(triadId))
                    {
                        var rowName = sheetNpcNames.GetRow(rowData.RowId);
                        if (rowName != null)
                        {
                            mapTriadNpcNames.Add(triadId, rowName.Singular.RawString);
                        }
                    }
                }
            }
            else
            {
                PluginLog.Fatal($"Failed to parse npc data (NN:{sheetNpcNames?.RowCount ?? 0}, NB:{sheetENpcBase?.RowCount ?? 0})");
                return false;
            }

            foreach (var rowData in npcDataSheet)
            {
                if (!mapTriadNpcNames.ContainsKey(rowData.RowId))
                {
                    // no name = no npc entry, disabled? skip it
                    continue;
                }

                var listRules = new List<TriadGameModifier>();
                if (rowData.TripleTriadRule != null)
                {
                    foreach (var ruleRow in rowData.TripleTriadRule)
                    {
                        if (ruleRow.Row != 0)
                        {
                            if (ruleRow.Row >= modDB.mods.Count)
                            {
                                PluginLog.Fatal($"Failed to parse npc data (rule.id:{ruleRow.Row})");
                                return false;
                            }

                            listRules.Add(modDB.mods[(int)ruleRow.Row]);
                        }
                    }
                }

                int numCardsFixed = 0;
                int[] cardsFixed = new int[5];
                if (rowData.TripleTriadCardFixed != null)
                {
                    if (rowData.TripleTriadCardFixed.Length != 5)
                    {
                        PluginLog.Fatal($"Failed to parse npc data (num CF:{rowData.TripleTriadCardFixed.Length})");
                        return false;
                    }

                    for (int cardIdx = 0; cardIdx < rowData.TripleTriadCardFixed.Length; cardIdx++)
                    {
                        var cardRowIdx = rowData.TripleTriadCardFixed[cardIdx].Row;
                        if (cardRowIdx != 0)
                        {
                            if (cardRowIdx >= cardDB.cards.Count)
                            {
                                PluginLog.Fatal($"Failed to parse npc data (card.id:{cardRowIdx})");
                                return false;
                            }

                            cardsFixed[cardIdx] = (int)cardRowIdx;
                            numCardsFixed++;
                        }
                    }
                }

                int numCardsVar = 0;
                int[] cardsVariable = new int[5];
                if (rowData.TripleTriadCardVariable != null)
                {
                    if (rowData.TripleTriadCardVariable.Length != 5)
                    {
                        PluginLog.Fatal($"Failed to parse npc data (num CV:{rowData.TripleTriadCardVariable.Length})");
                        return false;
                    }

                    for (int cardIdx = 0; cardIdx < rowData.TripleTriadCardVariable.Length; cardIdx++)
                    {
                        var cardRowIdx = rowData.TripleTriadCardVariable[cardIdx].Row;
                        if (cardRowIdx != 0)
                        {
                            if (cardRowIdx >= cardDB.cards.Count)
                            {
                                PluginLog.Fatal($"Failed to parse npc data (card.id:{cardRowIdx})");
                                return false;
                            }

                            cardsVariable[cardIdx] = (int)cardRowIdx;
                            numCardsVar++;
                        }
                    }
                }

                if (numCardsFixed == 0 && numCardsVar == 0)
                {
                    // no cards = disabled, skip it
                    continue;
                }

                var npcOb = new TriadNpc(npcDB.npcs.Count, listRules, cardsFixed, cardsVariable);
                npcOb.Name.Text = mapTriadNpcNames[rowData.RowId];
                npcDB.npcs.Add(npcOb);
            }

            return true;
        }
    }
}
