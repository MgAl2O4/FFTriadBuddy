using System;
using System.Drawing;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FormAdjustCard : Form
    {
        public ScannerTriad.CardState cardState;
        private bool skipNumUpdate = false;

        public FormAdjustCard()
        {
            InitializeComponent();
        }

        public void InitializeForCard(ScannerTriad.CardState cardState)
        {
            this.cardState = cardState;
        }

        private void FormAdjustCard_Load(object sender, EventArgs e)
        {
            labelCurrentCard.Text = string.Format("{0} [{1}-{2}-{3}-{4}]",
                (cardState.card != null) ? cardState.card.Name : "unknown",
                cardState.sideNumber[0], cardState.sideNumber[1], cardState.sideNumber[2], cardState.sideNumber[3]);

            skipNumUpdate = true;
            numericUpDownU.Value = cardState.sideNumber[0];
            numericUpDownL.Value = cardState.sideNumber[1];
            numericUpDownD.Value = cardState.sideNumber[2];
            numericUpDownR.Value = cardState.sideNumber[3];

            PictureBox[] hashPreviewBox = new PictureBox[4] { pictureBoxU, pictureBoxL, pictureBoxD, pictureBoxR };
            Label[] sideInfoLabel = new Label[4] { labelDescU, labelDescL, labelDescD, labelDescR };

            Size hashSize = ScannerTriad.GetDigitHashSize();
            for (int idx = 0; idx < 4; idx++)
            {
                ImageUtils.HashPreview hashPreview = new ImageUtils.HashPreview();
                hashPreview.bounds = new Rectangle(0, 0, hashSize.Width, hashSize.Height);
                hashPreview.hashValues = cardState.sideInfo[idx].hashValues;

                Bitmap bitmap = new Bitmap(hashSize.Width, hashSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                ImageUtils.DrawDebugHash(bitmap, hashPreview, Color.White);

                hashPreviewBox[idx].Image = bitmap;

                sideInfoLabel[idx].Text = cardState.sideInfo[idx].hasOverride ? "custom" : string.Format("auto: {0:P0}", cardState.sideInfo[idx].matchPct);
            }

            pictureBoxNumBox.Image = ImageUtils.CreatePreviewImage(cardState.sourceImage, cardState.bounds, Rectangle.Empty);

            skipNumUpdate = false;
            UpdateAdjustedCard();
        }

        private void UpdateAdjustedCard()
        {
            int sideU = (int)numericUpDownU.Value;
            int sideL = (int)numericUpDownL.Value;
            int sideD = (int)numericUpDownD.Value;
            int sideR = (int)numericUpDownR.Value;
            comboBox1.Items.Clear();

            TriadCard foundCard = TriadCardDB.Get().Find(sideU, sideL, sideD, sideR);
            if (foundCard != null)
            {
                if (foundCard.SameNumberId < 0)
                {
                    comboBox1.Items.Add(foundCard);
                }
                else
                {
                    foreach (TriadCard card in TriadCardDB.Get().sameNumberMap[foundCard.SameNumberId])
                    {
                        comboBox1.Items.Add(card);
                    }
                }

                comboBox1.SelectedIndex = 0;
            }

            buttonOk.Enabled = comboBox1.Items.Count > 0;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            if (comboBox1.SelectedItem != null)
            {
                TriadCard card = comboBox1.SelectedItem as TriadCard;

                // check modified numbers
                for (int idx = 0; idx < 4; idx++)
                {
                    if (card.Sides[idx] != cardState.sideNumber[idx])
                    {
                        ImageHashData digitPattern = new ImageHashData() { type = EImageHashType.CardNumber, previewBounds = cardState.sideInfo[idx].scanBox, previewContextBounds = cardState.scanBox, isKnown = true };
                        digitPattern.CalculateHash(cardState.sideInfo[idx].hashValues);
                        digitPattern.ownerOb = card.Sides[idx];

                        PlayerSettingsDB.Get().RemoveKnownHash(digitPattern);

                        if (card.Sides[idx] != cardState.sideInfo[idx].matchNum)
                        {
                            PlayerSettingsDB.Get().AddKnownHash(digitPattern);
                            cardState.sideInfo[idx].hasOverride = true;
                        }

                        cardState.sideNumber[idx] = card.Sides[idx];
                        cardState.card = card;
                        cardState.failedMatching = false;
                        DialogResult = DialogResult.OK;
                    }
                }

                // check multicard hash
                if (comboBox1.Items.Count > 1 &&
                    cardState.cardImageHash != null &&
                    cardState.card != card)
                {
                    cardState.cardImageHash.ownerOb = card;

                    PlayerSettingsDB.Get().RemoveKnownHash(cardState.cardImageHash);
                    PlayerSettingsDB.Get().AddKnownHash(cardState.cardImageHash);

                    cardState.card = card;
                    DialogResult = DialogResult.OK;
                }
            }

            Close();
        }

        private void numericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (!skipNumUpdate)
            {
                UpdateAdjustedCard();
            }
        }
    }
}
