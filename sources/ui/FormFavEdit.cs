using System;
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
            ApplyLocalization();

            deckCtrl1.clickAction = EDeckCtrlAction.Pick;
            deckCtrl1.allowRearrange = true;
        }

        private void ApplyLocalization()
        {
            Text = loc.strings.FavDeckForm_Title;
            label1.Text = loc.strings.FavDeckForm_Info;
            label2.Text = loc.strings.FavDeckForm_Name;
            buttonAdd.Text = loc.strings.FavDeckForm_AddButton;
            buttonRemove.Text = loc.strings.FavDeckForm_RemoveButton;
        }

        public void InitDeck(int slotIdx, TriadDeck copyFrom, ImageList cardImages, ImageList cardTypes, ImageList cardRarity)
        {
            this.slotIdx = slotIdx;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            if (slotIdx < playerDB.favDecks.Count)
            {
                deck = playerDB.favDecks[slotIdx];
                buttonAdd.Text = loc.strings.FavDeckForm_Dynamic_UpdateButton;
            }
            else
            {
                deck = new TriadDeckNamed(copyFrom);
                deck.Name = string.Format(loc.strings.FavDeckForm_Dynamic_AutoName, slotIdx + 1);
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
            DialogResult ret = MessageBox.Show(loc.strings.FavDeckForm_Dynamic_RemoveMsg, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ret == DialogResult.Yes)
            {
                DialogResult = DialogResult.No;
                PlayerSettingsDB.Get().UpdateFavDeck(slotIdx, null);

                Close();
            }
        }
    }
}
