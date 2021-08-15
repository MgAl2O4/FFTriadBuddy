using Dalamud.Plugin;
using FFTriadBuddy;
using System;
using System.Collections.Generic;

namespace TriadBuddyPlugin
{
    public class GameUI
    {
        public class State
        {
            public class Card
            {
                public byte numU;
                public byte numL;
                public byte numD;
                public byte numR;
                public byte rarity;
                public byte type;
                public byte owner;
                public bool isPresent;
                public bool isLocked;
                public string texturePath;

                public bool IsHidden => isPresent && (numU == 0);
            }

            public List<string> rules;
            public List<string> redPlayerDesc;
            public Card[] blueDeck = new Card[5];
            public Card[] redDeck = new Card[5];
            public Card[] board = new Card[9];
            public byte move;
        }

        public enum Status
        {
            NoErrors,
            AddonNotFound,
            AddonNotVisible,
            FailedToReadMove,
            FailedToReadRules,
            FailedToReadRedPlayer,
            FailedToReadCards,
            FailedToParseCards,
            FailedToParseRules,
            FailedToParseNpc,
        }

        public IntPtr addonPtr;
        public State currentState;
        public Status status;

        private DalamudPluginInterface pluginInterface;

        public GameUI(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public unsafe void Update()
        {
            addonPtr = pluginInterface.Framework.Gui.GetUiObjectByName("TripleTriad", 1);
            if (addonPtr == IntPtr.Zero)
            {
                SetStatus(Status.AddonNotFound);
                currentState = null;
                return;
            }

            var addon = (AddonTripleTriad*)addonPtr;

            bool isVisible = (addon->AtkUnitBase.RootNode != null) && addon->AtkUnitBase.RootNode->IsVisible;
            if (!isVisible)
            {
                SetStatus(Status.AddonNotVisible);
                currentState = null;
                return;
            }

            // set directly, function is for reporting errors
            status = Status.NoErrors;
            currentState = new State();

            (currentState.rules, currentState.redPlayerDesc) = GetUIDescriptions(addon);

            if (status == Status.NoErrors)
            {
                currentState.move = addon->TurnState;
                if (currentState.move > 2)
                {
                    SetStatus(Status.FailedToReadMove);
                }
            }

            if (status == Status.NoErrors)
            {
                currentState.blueDeck[0] = GetCardData(addon->BlueDeck0);
                currentState.blueDeck[1] = GetCardData(addon->BlueDeck1);
                currentState.blueDeck[2] = GetCardData(addon->BlueDeck2);
                currentState.blueDeck[3] = GetCardData(addon->BlueDeck3);
                currentState.blueDeck[4] = GetCardData(addon->BlueDeck4);

                currentState.redDeck[0] = GetCardData(addon->RedDeck0);
                currentState.redDeck[1] = GetCardData(addon->RedDeck1);
                currentState.redDeck[2] = GetCardData(addon->RedDeck2);
                currentState.redDeck[3] = GetCardData(addon->RedDeck3);
                currentState.redDeck[4] = GetCardData(addon->RedDeck4);

                currentState.board[0] = GetCardData(addon->Board0);
                currentState.board[1] = GetCardData(addon->Board1);
                currentState.board[2] = GetCardData(addon->Board2);
                currentState.board[3] = GetCardData(addon->Board3);
                currentState.board[4] = GetCardData(addon->Board4);
                currentState.board[5] = GetCardData(addon->Board5);
                currentState.board[6] = GetCardData(addon->Board6);
                currentState.board[7] = GetCardData(addon->Board7);
                currentState.board[8] = GetCardData(addon->Board8);
            }

            if (status != Status.NoErrors)
            {
                currentState = null;
            }
        }

        // TODO: draw overlay for move suggestion, check Dalamud's hover in inspector

        private bool SetStatus(Status newStatus)
        {
            bool changed = false;
            if (status != newStatus)
            {
                status = newStatus;
                changed = true;

                if (newStatus != Status.NoErrors &&
                    newStatus != Status.AddonNotFound &&
                    newStatus != Status.AddonNotVisible)
                {
                    PluginLog.Error("error: " + newStatus);
                }
            }

            return changed;
        }

        private unsafe (List<string>, List<string>) GetUIDescriptions(AddonTripleTriad* addon)
        {
            var listRuleDesc = new List<string>();
            var listRedDesc = new List<string>();

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(addon->AtkUnitBase.RootNode);

            var nodeRule0 = GUINodeUtils.PickNode(nodeArrL0, 4, 12);
            var nodeArrRule = GUINodeUtils.GetImmediateChildNodes(nodeRule0);
            if (nodeArrRule != null && nodeArrRule.Length == 5)
            {
                for (int idx = 0; idx < 4; idx++)
                {
                    var text = GUINodeUtils.GetNodeText(nodeArrRule[4 - idx]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        listRuleDesc.Add(text);
                    }
                }
            }
            else
            {
                SetStatus(Status.FailedToReadRules);
            }

            var nodeName0 = GUINodeUtils.PickNode(nodeArrL0, 6, 12);
            var nodeArrNameL1 = GUINodeUtils.GetImmediateChildNodes(nodeName0);
            var nodeNameL1 = GUINodeUtils.PickNode(nodeArrNameL1, 0, 5);
            var nodeArrNameL2 = GUINodeUtils.GetAllChildNodes(nodeNameL1);
            // there are multiple text boxes, for holding different combinations of name & titles?
            // idk, too lazy to investigate, grab everything inside
            int numParsed = 0;
            if (nodeArrNameL2 != null)
            {
                foreach (var testNode in nodeArrNameL2)
                {
                    var isVisible = (testNode != null) ? (testNode->Flags & 0x10) == 0x10 : false;
                    if (isVisible)
                    {
                        numParsed++;
                        var text = GUINodeUtils.GetNodeText(testNode);
                        if (!string.IsNullOrEmpty(text))
                        {
                            listRedDesc.Add(text);
                        }
                    }
                }
            }

            if (numParsed == 0)
            {
                SetStatus(Status.FailedToReadRedPlayer);
            }

            return (listRuleDesc, listRedDesc);
        }

        private unsafe (string, bool) GetCardTextureData(AddonTripleTriadCard addonCard)
        {
            // DragDrop Component
            // [1] Icon Component
            //     [0] Base Component <- locked out colors here
            //         [3] Image Node
            var nodeA = GUINodeUtils.PickChildNode(addonCard.CardDropControl, 1, 3);
            var nodeB = GUINodeUtils.PickChildNode(nodeA, 0, 2);
            var nodeC = GUINodeUtils.PickChildNode(nodeB, 3, 21);
            var texPath = GUINodeUtils.GetNodeTexturePath(nodeC);

            if (nodeC == null)
            {
                SetStatus(Status.FailedToReadCards);
            }

            bool isLocked = (nodeB != null) && (nodeB->MultiplyRed < 100);
            return (texPath, isLocked);
        }

        private unsafe State.Card GetCardData(AddonTripleTriadCard addonCard)
        {
            var resultOb = new State.Card();
            if (addonCard.HasCard)
            {
                resultOb.isPresent = true;
                resultOb.owner = addonCard.CardOwner;

                bool isKnown = (addonCard.NumSideU != 0);
                if (isKnown)
                {
                    resultOb.numU = addonCard.NumSideU;
                    resultOb.numL = addonCard.NumSideL;
                    resultOb.numD = addonCard.NumSideD;
                    resultOb.numR = addonCard.NumSideR;
                    resultOb.rarity = addonCard.CardRarity;
                    resultOb.type = addonCard.CardType;

                    (resultOb.texturePath, resultOb.isLocked) = GetCardTextureData(addonCard);
                }
            }

            return resultOb;
        }

        public TriadCard ConvertToTriadCard(State.Card card)
        {
            TriadCard resultOb = null;
            if (card.isPresent)
            {
                var cardsDB = TriadCardDB.Get();
                if (!card.IsHidden)
                {
                    // there's hardly any point in doing side comparison since plugin can access card id directly, but i still like it :<
                    var matchOb = cardsDB.Find(card.numU, card.numL, card.numD, card.numR);
                    if (matchOb != null)
                    {
                        if (matchOb.SameNumberId < 0)
                        {
                            resultOb = matchOb;
                        }
                        else
                        {
                            // ambiguous match, use texture for exact Id
                            resultOb = cardsDB.FindByTexture(card.texturePath);
                        }
                    }

                    if (resultOb == null && SetStatus(Status.FailedToParseCards))
                    {
                        PluginLog.Error($"failed to match card [{card.numU:X}-{card.numL:X}-{card.numD:X}-{card.numR:X}], tex:{card.texturePath}");
                    }
                }
                else
                {
                    resultOb = cardsDB.hiddenCard;
                }
            }

            return resultOb;
        }

        public ETriadCardOwner ConvertToTriadCardOwner(byte ownerValue)
        {
            return (ownerValue == 1) ? ETriadCardOwner.Blue :
                (ownerValue == 2) ? ETriadCardOwner.Red :
                ETriadCardOwner.Unknown;
        }

        public TriadNpc ConvertToTriadNpc(List<string> names)
        {
            TriadNpc resultOb = null;

            var npcsDB = TriadNpcDB.Get();
            foreach (var name in names)
            {
                // some names will be truncated in UI, e.g. 'Guhtwint of the Three...'
                // limit match to first 20 characters and hope that SE wil keep it unique
                string matchPattern = (name.Length > 20) ? name.Substring(0, 20) : name;

                var matchOb = npcsDB.FindByNameStart(matchPattern);
                if (matchOb != null)
                {
                    if (resultOb == null || resultOb == matchOb)
                    {
                        resultOb = matchOb;
                    }
                    else
                    {
                        if (SetStatus(Status.FailedToParseNpc))
                        {
                            PluginLog.Error($"failed to match npc: {string.Join(", ", names)}");
                        }

                        // um.. names matched two different npc, fail 
                        return null;
                    }
                }
            }

            return resultOb;
        }

        public List<TriadGameModifier> ConvertToTriadModifiers(List<string> rules)
        {
            var list = new List<TriadGameModifier>();

            var modsDB = TriadGameModifierDB.Get();
            foreach (var rule in rules)
            {
                var matchOb = modsDB.mods.Find(x => x.GetLocalizedName().Equals(rule, StringComparison.OrdinalIgnoreCase));
                if (matchOb != null)
                {
                    list.Add(matchOb);
                }
                else
                {
                    if (SetStatus(Status.FailedToParseRules))
                    {
                        PluginLog.Error($"failed to match rule: {rule}");
                    }
                }
            }

            return list;
        }
    }
}
