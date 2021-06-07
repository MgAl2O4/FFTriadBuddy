using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FormAdjustHash : Form
    {
        public class HashOwnerItem : IComparable
        {
            public object SourceObject;
            string Description;

            public HashOwnerItem(TriadGameModifier mod)
            {
                SourceObject = mod;
                Description = mod.GetLocalizedName();
            }

            public HashOwnerItem(TriadCard card)
            {
                SourceObject = card;
                Description = card.Name.GetLocalized();
            }

            public HashOwnerItem(int number)
            {
                SourceObject = number;
                Description = (number == 10) ? "A" : number.ToString();
            }

            public int CompareTo(object obj)
            {
                return Description.CompareTo(obj.ToString());
            }

            public override string ToString()
            {
                return Description;
            }

            public static HashOwnerItem CreateFrom(ImageHashData hashData)
            {
                switch (hashData.type)
                {
                    case EImageHashType.Rule:
                        return new HashOwnerItem((TriadGameModifier)hashData.ownerOb);

                    case EImageHashType.CardImage:
                        return new HashOwnerItem((TriadCard)hashData.ownerOb);

                    case EImageHashType.CardNumber:
                        return new HashOwnerItem((int)hashData.ownerOb);

                    case EImageHashType.Cactpot:
                        return new HashOwnerItem((int)hashData.ownerOb);

                    default: break;
                }

                return null;
            }
        }

        public ImageHashData hashData;

        public FormAdjustHash()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            Text = loc.strings.AdjustForm_Title;
            label1.Text = loc.strings.AdjustForm_Current;
            label4.Text = loc.strings.AdjustForm_Distance;
            label2.Text = loc.strings.AdjustForm_HashList;
            buttonOk.Text = loc.strings.AdjustForm_SaveButton;
            buttonCancel.Text = loc.strings.AdjustForm_CancelButton;
        }

        static public List<object> GenerateHashOwnerOptions(ImageHashData hashData)
        {
            List<object> list = new List<object>();
            switch (hashData.type)
            {
                case EImageHashType.Rule:
                    foreach (TriadGameModifier mod in ImageHashDB.Get().modObjects)
                    {
                        list.Add(new HashOwnerItem(mod));
                    }
                    break;

                case EImageHashType.CardImage:
                    foreach (TriadCard card in TriadCardDB.Get().sameNumberMap[((TriadCard)hashData.ownerOb).SameNumberId])
                    {
                        list.Add(new HashOwnerItem(card));
                    }
                    break;

                case EImageHashType.CardNumber:
                    for (int number = 1; number <= 10; number++)
                    {
                        list.Add(new HashOwnerItem(number));
                    }
                    break;

                case EImageHashType.Cactpot:
                    for (int number = 1; number <= 9; number++)
                    {
                        list.Add(new HashOwnerItem(number));
                    }
                    break;

                default: break;
            }

            return list;
        }

        public void InitializeForHash(ImageHashData hashData)
        {
            this.hashData = hashData;
        }

        private void FormAdjustHash_Load(object sender, EventArgs e)
        {
            hashData.UpdatePreviewImage();
            var typedOwner = HashOwnerItem.CreateFrom(hashData);

            Size orgSizeImageBox = pictureBox1.Size;
            Size orgSizeForm = Size;
            Size newSizeImage = hashData.previewImage.Size;
            if (newSizeImage.Width > orgSizeImageBox.Width || newSizeImage.Height > orgSizeImageBox.Height)
            {
                int SizeDX = Math.Max(0, newSizeImage.Width - orgSizeImageBox.Width);
                int SizeDY = Math.Max(0, newSizeImage.Height - orgSizeImageBox.Height);
                Size = new Size(orgSizeForm.Width + SizeDX, orgSizeForm.Height + SizeDY);
            }

            labelHashOrg.Text = (typedOwner != null) ? typedOwner.ToString() : loc.strings.AdjustForm_Dynamic_UnknownOwner;
            labelDistance.Text = hashData.isAuto ? loc.strings.AdjustForm_Dynamic_Distance_NotAvail : hashData.matchDistance.ToString();
            labelDescDistance.Text = hashData.isAuto ? loc.strings.AdjustForm_Dynamic_Distance_Classifier :
                hashData.matchDistance == 0 ? loc.strings.AdjustForm_Dynamic_Distance_Exact :
                loc.strings.AdjustForm_Dynamic_Distance_DefaultHint;
            pictureBox1.Image = hashData.previewImage;

            comboBoxOwner.Items.Clear();
            comboBoxOwner.Items.AddRange(GenerateHashOwnerOptions(hashData).ToArray());

            bool foundSelection = false;
            for (int idx = 0; idx < comboBoxOwner.Items.Count; idx++)
            {
                HashOwnerItem hashComboItem = (HashOwnerItem)comboBoxOwner.Items[idx];
                if (hashComboItem.SourceObject == hashData.ownerOb ||
                    hashComboItem.SourceObject.ToString() == labelHashOrg.Text)
                {
                    comboBoxOwner.SelectedIndex = idx;
                    foundSelection = true;
                    break;
                }
            }

            if (!foundSelection) { comboBoxOwner_SelectedIndexChanged(null, null); }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            HashOwnerItem hashComboItem = (HashOwnerItem)comboBoxOwner.SelectedItem;
            if (hashComboItem != null)
            {
                if (hashData.ownerOb == null || (labelHashOrg.Text != hashComboItem.SourceObject.ToString()))
                {
                    PlayerSettingsDB.Get().RemoveKnownHash(hashData);
                    hashData.ownerOb = hashComboItem.SourceObject;
                    hashData.matchDistance = 0;
                    hashData.isAuto = false;

                    PlayerSettingsDB.Get().AddKnownHash(hashData);
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void comboBoxOwner_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool canSave = false;
            if (comboBoxOwner.SelectedItem != null)
            {
                HashOwnerItem hashComboItem = (HashOwnerItem)comboBoxOwner.SelectedItem;
                canSave = labelHashOrg.Text != hashComboItem.SourceObject.ToString();
            }

            buttonOk.Enabled = canSave;
        }
    }
}
