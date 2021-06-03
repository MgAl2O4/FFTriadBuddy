using System;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FavDeckCtrl : UserControl
    {
        public delegate void SimpleDelegate(int TagIdx);
        public event SimpleDelegate OnEdit;
        public event SimpleDelegate OnUse;

        public FavDeckCtrl()
        {
            InitializeComponent();

            deckCtrl.drawMode = ECardDrawMode.ImageOnly;
            deckCtrl.allowRearrange = false;
            deckCtrl.enableHitTest = false;
            deckCtrl.enableLocking = false;
            deckCtrl.clickAction = EDeckCtrlAction.None;
            deckCtrl.SetCardSize(29, 5);
        }

        public void SetImageLists(ImageList cardImages, ImageList typeImages, ImageList rarityImages)
        {
            deckCtrl.cardIcons = cardImages;
            deckCtrl.cardTypes = typeImages;
            deckCtrl.cardRarity = rarityImages;
        }

        public void SetDeck(TriadDeckNamed deckInfo)
        {
            deckCtrl.Visible = (deckInfo != null);
            buttonUse.Visible = (deckInfo != null);
            buttonEdit.Visible = (deckInfo != null);
            labelTitle.Visible = (deckInfo != null);
            labelChance.Visible = (deckInfo != null);

            if (deckInfo != null)
            {
                bool deckChanged = (deckCtrl.deck == null) || !deckCtrl.deck.Equals(deckInfo);

                deckCtrl.SetDeck(deckInfo);
                labelTitle.Text = deckInfo.Name;
                buttonEdit.Text = loc.strings.FavDeckCtrl_Edit;

                if (deckChanged)
                {
                    labelChance.Text = "...";
                }
            }
        }

        public void SetLocked(bool lockMe)
        {
            buttonEdit.Enabled = !lockMe;
            buttonUse.Enabled = !lockMe;
        }

        public void UpdateChance(TriadGameResultChance chance)
        {
            labelChance.Text = chance.winChance.ToString("P2");
        }

        private void buttonUse_Click(object sender, EventArgs e)
        {
            int TagIdx = (int)Tag;
            OnUse?.Invoke(TagIdx);
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            int TagIdx = (int)Tag;
            OnEdit?.Invoke(TagIdx);
        }
    }
}
