using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public enum EDeckCtrlAction
    {
        None,
        Highlight,
        Pick,
    }

    public partial class DeckCtrl : UserControl
    {
        private class DeckReorderMarker
        {
        }

        private class DeckCardPicker
        {
            public readonly TriadCard Card;

            public DeckCardPicker(TriadCard card)
            {
                Card = card;
            }

            public override string ToString()
            {
                string rarityStr = "*";
                for (ETriadCardRarity Idx = ETriadCardRarity.Common; Idx < Card.Rarity; Idx++)
                {
                    rarityStr += " *";
                }

                return Card.Name + " (" + rarityStr + ")";
            }
        }

        public ImageList cardIcons;
        public ImageList cardTypes;
        public ImageList cardRarity;
        public TriadDeck deck;
        public ETriadCardOwner deckOwner;

        public ECardDrawMode drawMode;
        public bool allowRearrange;
        public bool enableHitTest;
        public bool enableLocking;
        public EDeckCtrlAction clickAction;

        public delegate void DeckChangedDelegate(TriadCardInstance cardInst, int slotIdx, bool previewOnly);
        public delegate void DeckSelectDelegate(TriadCardInstance cardInst, int slotIdx);
        public delegate void DeckRearranged(int slotIdx1, int slotIdx2);
        public event DeckChangedDelegate OnCardChanged;
        public event DeckSelectDelegate OnCardSelected;
        public event DeckRearranged OnDeckRearranged;

        private int dragTargetIdx;
        private int dragSourceIdx;
        private CardCtrl[] cardCtrls;
        private CardCtrl cardClickOwner;
        private bool[] lockFlags;

        public DeckCtrl()
        {
            drawMode = ECardDrawMode.Detailed;
            allowRearrange = true;
            enableHitTest = true;
            enableLocking = false;
            clickAction = EDeckCtrlAction.Pick;
            deckOwner = ETriadCardOwner.Blue;

            InitializeComponent();

            toolStripMenuOnlyOwned.Checked = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HitInvisConst.WM_NCHITTEST && !enableHitTest)
                m.Result = (IntPtr)HitInvisConst.HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }

        public void SetDeck(TriadDeck deck)
        {
            if ((clickAction == EDeckCtrlAction.Highlight) && (cardClickOwner != null))
            {
                cardClickOwner.SetHighlighted(false);
                cardClickOwner = null;
            }

            int prevCtrlCount = (cardCtrls != null) ? cardCtrls.Length : 0;
            int numCards = deck.knownCards.Count + deck.unknownCardPool.Count;
            if (prevCtrlCount == numCards)
            {
                this.deck = null;
                for (int Idx = 0; Idx < cardCtrls.Length; Idx++)
                {
                    setDeckCard(Idx, deck.GetCard(Idx), true);
                }

                this.deck = deck;
                return;
            }

            if (deck.unknownCardPool.Count == 0)
            {
                SetDeck(deck.knownCards);
            }
            else
            {
                List<TriadCard> allCards = new List<TriadCard>();
                allCards.AddRange(deck.knownCards);
                allCards.AddRange(deck.unknownCardPool);
                SetDeck(allCards);
            }

            this.deck = deck;
        }

        public void SetDeck(IEnumerable<TriadCard> knownCards)
        { 
            if ((clickAction == EDeckCtrlAction.Highlight) && (cardClickOwner != null))
            {
                cardClickOwner.SetHighlighted(false);
                cardClickOwner = null;
            }
            SuspendLayout();

            if (cardCtrls != null)
            {
                foreach (CardCtrl control in cardCtrls)
                {
                    Controls.Remove(control);
                }
            }

            int numCards = knownCards.Count();
            cardCtrls = new CardCtrl[numCards];
            int Idx = 0;

            if (lockFlags == null || lockFlags.Length != numCards)
            {
                lockFlags = new bool[numCards];
            }

            foreach (TriadCard knownCard in knownCards)
            {
                CardCtrl newCardCtrl = new CardCtrl();
                cardCtrls[Idx] = newCardCtrl;

                newCardCtrl.cardIcons = cardIcons;
                newCardCtrl.cardTypes = cardTypes;
                newCardCtrl.cardRarity = cardRarity;
                newCardCtrl.Tag = Idx;
                newCardCtrl.defaultBackColor = BackColor;

                newCardCtrl.Location = new Point(56 * Idx, 0);
                newCardCtrl.Size = new Size(50, 50);
                newCardCtrl.BorderStyle = BorderStyle.FixedSingle;
                newCardCtrl.AllowDrop = true;
                newCardCtrl.bBlinkHighlighted = (clickAction != EDeckCtrlAction.Highlight);
                newCardCtrl.bEnableHitTest = enableHitTest;
                newCardCtrl.bIsLocked = lockFlags[Idx];
                newCardCtrl.drawMode = drawMode;

                if (clickAction != EDeckCtrlAction.None)
                {
                    newCardCtrl.Click += CardCtrl_Click;
                }
                if (allowRearrange)
                {
                    newCardCtrl.MouseMove += CardCtrl_MouseMove;
                    newCardCtrl.DragEnter += CardCtrl_DragEnter;
                    newCardCtrl.DragLeave += CardCtrl_DragLeave;
                    newCardCtrl.DragDrop += CardCtrl_DragDrop;
                }

                setDeckCard(Idx, knownCard, true);
                Controls.Add(newCardCtrl);
                Idx++;
            }

            ResumeLayout();
            Invalidate();
        }

        public bool GetActiveCard(out int slotIdx, out TriadCard card)
        {
            bool bResult = false;
            slotIdx = -1;
            card = null;

            if (deck != null && cardClickOwner != null)
            {
                slotIdx = (int)cardClickOwner.Tag;
                card = deck.GetCard(slotIdx);
                bResult = true;
            }

            return bResult;
        }

        public bool HasActiveCard()
        {
            return (deck != null && cardClickOwner != null);
        }

        private void CardCtrl_DragDrop(object sender, DragEventArgs e)
        {
            if (dragSourceIdx != dragTargetIdx)
            {
                if (OnDeckRearranged != null)
                {
                    OnDeckRearranged.Invoke(dragSourceIdx, dragTargetIdx);
                }
            }
        }

        private void CardCtrl_DragLeave(object sender, EventArgs e)
        {
            if (dragSourceIdx != dragTargetIdx)
            {
                TriadCard cardFrom = deck.GetCard(dragSourceIdx);
                TriadCard cardTo = deck.GetCard(dragTargetIdx);
                setDeckCard(dragSourceIdx, cardTo, true);
                setDeckCard(dragTargetIdx, cardFrom, true);
            }
        }

        private void CardCtrl_DragEnter(object sender, DragEventArgs e)
        {
            int slotIdx = (int)((CardCtrl)sender).Tag;
            if (e.Data.GetDataPresent(typeof(DeckReorderMarker)) && (dragTargetIdx != slotIdx))
            {
                e.Effect = DragDropEffects.Move;
                dragTargetIdx = slotIdx;

                TriadCard cardFrom = deck.GetCard(dragSourceIdx);
                TriadCard cardTo = deck.GetCard(dragTargetIdx);
                setDeckCard(dragSourceIdx, cardTo, true);
                setDeckCard(dragTargetIdx, cardFrom, true);
            }
        }

        private void CardCtrl_MouseMove(object sender, MouseEventArgs e)
        {
            CardCtrl cardCtrlSender = (CardCtrl)sender;
            if (e.Button == MouseButtons.Left && allowRearrange && (deck != null))
            {
                dragTargetIdx = dragSourceIdx = (int)cardCtrlSender.Tag;

                DeckReorderMarker dragData = new DeckReorderMarker();
                DoDragDrop(dragData, DragDropEffects.Move);
            }
        }

        private void CardCtrl_Click(object sender, EventArgs e)
        {
            CardCtrl prevOwner = cardClickOwner;

            cardClickOwner = (CardCtrl)sender;
            if (cardClickOwner != null)
            {
                if (clickAction == EDeckCtrlAction.Pick)
                {
                    toolStripMenuLockOptimization.Checked = lockFlags[(int)cardClickOwner.Tag];
                    contextMenuStripPickCard.Show(cardClickOwner, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
                }
                else if (clickAction == EDeckCtrlAction.Highlight)
                {
                    if (prevOwner != null)
                    {
                        prevOwner.SetHighlighted(false);
                    }

                    if (OnCardSelected != null)
                    {
                        OnCardSelected.Invoke(cardClickOwner.GetCardInst(), (int)cardClickOwner.Tag);
                    }

                    if (prevOwner == cardClickOwner)
                    {
                        cardClickOwner = null;
                    }
                    else
                    {
                        cardClickOwner.SetHighlighted(true);
                    }
                }
            }
        }

        private void contextMenuStripPickCard_Opened(object sender, EventArgs e)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            TriadCardDB cardsDB = TriadCardDB.Get();
            bool bShouldRefresh = toolStripMenuOnlyOwned.Checked || (cardsDB.cards.Count != toolStripComboBoxPick.Items.Count);
            if (bShouldRefresh)
            {
                toolStripComboBoxPick.Items.Clear();
                foreach (TriadCard card in cardsDB.cards)
                {
                    if (card != null && (!toolStripMenuOnlyOwned.Checked || playerDB.ownedCards.Contains(card)))
                    {
                        toolStripComboBoxPick.Items.Add(new DeckCardPicker(card));
                    }
                }
            }

            for (int Idx = 0; Idx < toolStripComboBoxPick.Items.Count; Idx++)
            {
                DeckCardPicker cardOb = (DeckCardPicker)toolStripComboBoxPick.Items[Idx];
                if (cardOb.Card == cardClickOwner.GetCard())
                {
                    toolStripComboBoxPick.SelectedIndex = Idx;
                }
            }

            toolStripComboBoxPick.Focus();
        }

        private void contextMenuStripPickCard_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void toolStripMenuOnlyOwned_CheckedChanged(object sender, EventArgs e)
        {
            toolStripComboBoxPick.Items.Clear();

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            TriadCardDB cardsDB = TriadCardDB.Get();

            foreach (TriadCard card in cardsDB.cards)
            {
                if (card != null && (!toolStripMenuOnlyOwned.Checked || playerDB.ownedCards.Contains(card)))
                {
                    toolStripComboBoxPick.Items.Add(new DeckCardPicker(card));
                }
            }
        }

        private void toolStripMenuLockOptimization_CheckedChanged(object sender, EventArgs e)
        {
            int Idx = (int)cardClickOwner.Tag;
            lockFlags[Idx] = toolStripMenuLockOptimization.Checked;
            cardClickOwner.SetLockedMode(lockFlags[Idx]);
        }

        private void toolStripComboBoxPick_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeckCardPicker selectedCardOb = (DeckCardPicker)toolStripComboBoxPick.SelectedItem;
            if (cardClickOwner.GetCard() != selectedCardOb.Card)
            {
                setDeckCard((int)cardClickOwner.Tag, selectedCardOb.Card, false);
            }
        }

        private void setDeckCard(int Idx, TriadCard card, bool previewOnly)
        {
            if (deck != null)
            {
                deck.SetCard(Idx, card);
            }

            CardCtrl updatedCtrl = cardCtrls[Idx];
            if (updatedCtrl.GetCard() != card)
            {
                updatedCtrl.SetCard(new TriadCardInstance(card, deckOwner));
            }

            if (OnCardChanged != null)
            {
                OnCardChanged.Invoke(cardCtrls[Idx].GetCardInst(), Idx, previewOnly);
            }

            updatedCtrl.UpdateBackColor();
            updatedCtrl.Invalidate();
        }

        public bool IsLocked(int Idx)
        {
            return lockFlags[Idx];
        }

        public List<TriadCard> GetLockedCards()
        {
            List<TriadCard> cards = new List<TriadCard>();
            if (deck != null)
            {
                for (int Idx = 0; Idx < lockFlags.Length; Idx++)
                {
                    cards.Add(lockFlags[Idx] ? deck.GetCard(Idx) : null);
                }
            }

            return cards;
        }

        public bool IsMatching(IEnumerable<TriadCard> cards)
        {
            if (cardCtrls == null)
            {
                return false;
            }

            int NumMisses = cardCtrls.Length;
            int Idx = 0;
            foreach (TriadCard testCard in cards)
            {
                if (Idx < cardCtrls.Length)
                {
                    if (cardCtrls[Idx].GetCard() == testCard)
                    {
                        NumMisses--;
                    }
                }
                else
                {
                    NumMisses++;
                }
            }

            return NumMisses == 0;
        }
    }
}