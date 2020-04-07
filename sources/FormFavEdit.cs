using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FormFavEdit : Form
    {
        public TriadDeckNamed deck;
        public int slotIdx;

        public FormFavEdit()
        {
            InitializeComponent();

            deckCtrl1.clickAction = EDeckCtrlAction.Pick;
            deckCtrl1.allowRearrange = true;
        }

        public void InitDeck(int slotIdx, TriadDeck copyFrom, ImageList cardImages, ImageList cardTypes, ImageList cardRarity)
        {
            this.slotIdx = slotIdx;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            if (slotIdx < playerDB.favDecks.Count)
            {
                deck = playerDB.favDecks[slotIdx];
                buttonAdd.Text = "Update";
            }
            else
            {
                deck = new TriadDeckNamed(copyFrom);
                deck.Name = "Fav #" + (slotIdx + 1).ToString();
            }

            deckCtrl1.SetImageLists(cardImages, cardTypes, cardRarity);
            deckCtrl1.SetDeck(deck);
            textBox1.Text = deck.Name;
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes;

            deck.Name = textBox1.Text;
            PlayerSettingsDB.Get().UpdateFavDeck(slotIdx, deck);

            Close();
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            DialogResult ret = MessageBox.Show("Favorite deck will be removed, do you want to continue?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ret == DialogResult.Yes)
            {
                DialogResult = DialogResult.No;
                PlayerSettingsDB.Get().UpdateFavDeck(slotIdx, null);

                Close();
            }
        }
    }
}
