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
    public partial class CardGridCtrl : UserControl
    {
        public ImageList cardIcons;
        private CardCtrl[] cardControls;

        public delegate void GridClickedDelegate(TriadCard card);
        public event GridClickedDelegate OnCardClicked;

        public CardGridCtrl()
        {
            InitializeComponent();

            cardControls = new CardCtrl[] {
                cardCtrl1, cardCtrl2, cardCtrl3, cardCtrl4, cardCtrl5,
                cardCtrl6, cardCtrl7, cardCtrl8, cardCtrl9, cardCtrl10,
                cardCtrl11, cardCtrl12, cardCtrl13, cardCtrl14, cardCtrl15,
                cardCtrl16, cardCtrl17, cardCtrl18, cardCtrl19, cardCtrl20,
                cardCtrl21, cardCtrl22, cardCtrl23, cardCtrl24, cardCtrl25,
            };
        }

        public void InitCardControls()
        { 
            for (int Idx = 0; Idx < cardControls.Length; Idx++)
            {
                cardControls[Idx].drawMode = ECardDrawMode.ImageOnly;
                cardControls[Idx].cardIcons = cardIcons;
                cardControls[Idx].bBlinkHighlighted = false;
                cardControls[Idx].Tag = Idx;
                cardControls[Idx].SetCard(null);
                cardControls[Idx].Click += CardGridCtrl_Click;
            }
        }

        private void CardGridCtrl_Click(object sender, EventArgs e)
        {
            CardCtrl cardControl = sender as CardCtrl;
            MouseEventArgs mouseArgs = e as MouseEventArgs;
            if (cardControl != null && cardControl.GetCard() != null && 
                (mouseArgs == null || mouseArgs.Button == MouseButtons.Left))
            {
                OnCardClicked(cardControl.GetCard());
            }
        }

        public void Clear()
        {
            foreach (CardCtrl cardCtrl in cardControls)
            {
                cardCtrl.SetCard(null);
                cardCtrl.bIsTransparent = false;
            }
        }

        public void SetCard(int slotIdx, TriadCard card, bool bIsOwned)
        {
            cardControls[slotIdx].SetCard(new TriadCardInstance(card, ETriadCardOwner.Unknown));
            cardControls[slotIdx].bIsTransparent = !bIsOwned;
        }

        public void UpdateOwnedCard(TriadCard card, bool bIsOwned)
        {
            foreach (CardCtrl cardCtrl in cardControls)
            {
                if (cardCtrl.GetCard() == card)
                {
                    cardCtrl.bIsTransparent = !bIsOwned;
                    cardCtrl.Invalidate();
                    break;
                }
            }
        }
    }
}
