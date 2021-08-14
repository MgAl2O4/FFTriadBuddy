using Dalamud.Plugin;
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
            }

            public List<string> rules;
            public List<string> redPlayerDesc;
            public Card[] blueDeck = new Card[5];
            public Card[] redDeck = new Card[5];
            public Card[] board = new Card[9];
            public byte move;
        }

        public IntPtr addonPtr;
        public State currentState;

        private DalamudPluginInterface pluginInterface;

        public GameUI(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public unsafe void Update()
        {
            addonPtr = pluginInterface.Framework.Gui.GetUiObjectByName("TripleTriad", 1);
            AddonTripleTriad* addon = (addonPtr != IntPtr.Zero) ? (AddonTripleTriad*)addonPtr : null;

            bool isVisible = (addon != null) && (addon->AtkUnitBase.RootNode != null) && addon->AtkUnitBase.RootNode->IsVisible;
            if (isVisible)
            {
                currentState = new State();
                (currentState.rules, currentState.redPlayerDesc) = GetUIDescriptions(addon);
                currentState.move = addon->TurnState;

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
            else
            {
                currentState = null;
            }
        }

        // TODO: draw overlay for move suggestion, check Dalamud's hover in inspector

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

            var nodeName0 = GUINodeUtils.PickNode(nodeArrL0, 6, 12);
            var nodeArrNameL1 = GUINodeUtils.GetImmediateChildNodes(nodeName0);
            var nodeNameL1 = GUINodeUtils.PickNode(nodeArrNameL1, 0, 5);
            var nodeArrNameL2 = GUINodeUtils.GetAllChildNodes(nodeNameL1);
            // there are multiple text boxes, for holding different combinations of name & titles?
            // idk, too lazy to investigate, grab everything inside
            if (nodeArrNameL2 != null)
            {
                foreach (var testNode in nodeArrNameL2)
                {
                    var isVisible = (testNode != null) ? (testNode->Flags & 0x10) == 0x10 : false;
                    var text = isVisible ? GUINodeUtils.GetNodeText(testNode) : null;
                    if (!string.IsNullOrEmpty(text))
                    {
                        listRedDesc.Add(text);
                    }
                }
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

            bool isLocked = (nodeB != null) && (nodeB->MultiplyRed < 100);
            return (texPath, isLocked);
        }

        private unsafe State.Card GetCardData(AddonTripleTriadCard addonCard)
        {
            var resultOb = new State.Card();
            if (addonCard.HasCard)
            {
                resultOb.isPresent = true;

                bool isKnown = (addonCard.NumSideU != 0);
                if (isKnown)
                {
                    resultOb.numU = addonCard.NumSideU;
                    resultOb.numL = addonCard.NumSideL;
                    resultOb.numD = addonCard.NumSideD;
                    resultOb.numR = addonCard.NumSideR;
                    resultOb.rarity = addonCard.CardRarity;
                    resultOb.type = addonCard.CardType;
                    resultOb.owner = addonCard.CardOwner;

                    (resultOb.texturePath, resultOb.isLocked) = GetCardTextureData(addonCard);
                }
            }

            return resultOb;
        }
    }
}
