using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public enum ECardDrawMode
    {
        Detailed,
        OwnerOnly,
        ImageOnly,
    }

    public partial class CardCtrl : UserControl
    {
        private TriadCardInstance cardData;
        public ImageList cardIcons;
        public ImageList cardTypes;
        public ImageList cardRarity;
        private bool bIsHighlighted;
        private bool bDrawHighlighted;
        public bool bBlinkHighlighted;
        public bool bEnableHitTest;
        public bool bIsLocked;
        public bool bIsTransparent;
        public ECardDrawMode drawMode;

        private Font drawFont;
        private Brush fontBrush;
        private Brush fontBrushModPlus;
        private Brush fontBrushModMinus;
        private Brush shadowBrush;

        private static Color colorBlue = Color.FromArgb(0x87, 0xce, 0xfa);
        private static Color colorBlueHL = Color.FromArgb(0x05, 0x4f, 0x7d);
        private static Color colorRed = Color.FromArgb(0xfa, 0x87, 0x95);
        private static Color colorRedHL = Color.FromArgb(0x7d, 0x05, 0x13);
        public Color defaultBackColor = SystemColors.Control;

        public CardCtrl()
        {
            drawFont = new Font(FontFamily.GenericMonospace, 8.0f);
            fontBrush = new SolidBrush(Color.White);
            fontBrushModPlus = new SolidBrush(Color.FromArgb(0x3b, 0xff, 0x3b));
            fontBrushModMinus = new SolidBrush(Color.FromArgb(0xff, 0x3b, 0x3b));
            shadowBrush = new SolidBrush(Color.Black);

            InitializeComponent();

            SetCard(null);
            bIsLocked = false;
            bIsHighlighted = false;
            bIsTransparent = false;
            bDrawHighlighted = false;
            bBlinkHighlighted = true;
            bEnableHitTest = true;
            drawMode = ECardDrawMode.Detailed;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HitInvisConst.WM_NCHITTEST && !bEnableHitTest)
                m.Result = (IntPtr)HitInvisConst.HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }

        public void SetCard(TriadCardInstance cardData)
        {
            this.cardData = cardData;
            if (cardData != null)
            {
                toolTip1.SetToolTip(this, cardData.card.ToShortString());
            }
            else
            {
                toolTip1.SetToolTip(this, "");
            }

            UpdateBackColor();
        }

        public TriadCard GetCard()
        {
            return (cardData != null) ? cardData.card : null;
        }

        public ETriadCardOwner GetOwner()
        {
            return (cardData != null) ? cardData.owner : ETriadCardOwner.Unknown;
        }

        public TriadCardInstance GetCardInst()
        {
            return cardData;
        }

        public void SetHighlighted(bool bEnable)
        {
            timer1.Enabled = bBlinkHighlighted && bEnable;
            bDrawHighlighted = bEnable;
            bIsHighlighted = bEnable;
            UpdateBackColor();
            Invalidate();
        }

        public bool IsHighlighted()
        {
            return bIsHighlighted;
        }

        public void SetLockedMode(bool bLocked)
        {
            bIsLocked = bLocked;
            UpdateBackColor();
            Invalidate();
        }

        public void UpdateBackColor()
        {
            BackColor = defaultBackColor;

            if (bIsLocked)
            {
                BackColor = Color.Gray;
            }
            else if (cardData != null)
            {
                BackColor = (cardData.owner == ETriadCardOwner.Blue) ?
                    (bDrawHighlighted ? colorBlueHL : bIsHighlighted ? defaultBackColor : colorBlue) :
                    (bDrawHighlighted ? colorRedHL : bIsHighlighted ? defaultBackColor : colorRed);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            bDrawHighlighted = !bDrawHighlighted;

            UpdateBackColor();
            Invalidate();
        }

        private void DrawShadowedNum(Graphics g, int number, float posX, float posY)
        {
            string desc = (number == 10) ? "A" : number.ToString();
            DrawShadowedString(g, desc, posX, posY, fontBrush);
        }

        private void DrawShadowedString(Graphics g, string desc, float posX, float posY, Brush drawBrush)
        {
            g.DrawString(desc, drawFont, shadowBrush, posX - 1, posY);
            g.DrawString(desc, drawFont, shadowBrush, posX + 1, posY);
            g.DrawString(desc, drawFont, shadowBrush, posX, posY - 1);
            g.DrawString(desc, drawFont, shadowBrush, posX, posY + 1);
            g.DrawString(desc, drawFont, drawBrush, posX, posY);
        }

        private void CardCtrl_Paint(object sender, PaintEventArgs e)
        {
            if (cardData != null)
            {
                int cardDrawOffset = (drawMode == ECardDrawMode.ImageOnly) ? 0 : 5;
                Rectangle destImageRect = new Rectangle(cardDrawOffset, cardDrawOffset, e.ClipRectangle.Width - (cardDrawOffset * 2), e.ClipRectangle.Height - (cardDrawOffset * 2));

                if (cardData.card.Id != 0)
                {
                    Image cardImage = cardIcons.Images[cardData.card.Id];
                    if (bIsTransparent)
                    {
                        ColorMatrix colormatrix = new ColorMatrix();
                        colormatrix.Matrix33 = 0.4f;
                        ImageAttributes imgAttribute = new ImageAttributes();
                        imgAttribute.SetColorMatrix(colormatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                        e.Graphics.DrawImage(cardImage, destImageRect, 0, 0, cardImage.Width, cardImage.Height, GraphicsUnit.Pixel, imgAttribute);
                    }
                    else
                    {
                        e.Graphics.DrawImage(cardImage, destImageRect, 0, 0, cardImage.Width, cardImage.Height, GraphicsUnit.Pixel);
                    }
                }
                else
                {
                    Color hiddenCardColor = bIsTransparent ? Color.FromArgb(127, Color.DarkGoldenrod) : Color.DarkGoldenrod;
                    Brush hiddenCard = new SolidBrush(hiddenCardColor);
                    e.Graphics.FillRectangle(hiddenCard, destImageRect);
                }

                if (drawMode == ECardDrawMode.Detailed)
                { 
                    SizeF sizeChar = e.Graphics.MeasureString("X", drawFont);

                    float drawPad = 0.0f;
                    float drawMidX = (e.ClipRectangle.Width - sizeChar.Width) / 2.0f;
                    float drawMidY = (e.ClipRectangle.Height - sizeChar.Height) / 2.0f;

                    DrawShadowedNum(e.Graphics, cardData.GetRawNumber(ETriadGameSide.Up), drawMidX, drawPad);
                    DrawShadowedNum(e.Graphics, cardData.GetRawNumber(ETriadGameSide.Down), drawMidX, e.ClipRectangle.Bottom - sizeChar.Height - drawPad);
                    DrawShadowedNum(e.Graphics, cardData.GetRawNumber(ETriadGameSide.Right), drawPad, drawMidY);
                    DrawShadowedNum(e.Graphics, cardData.GetRawNumber(ETriadGameSide.Left), e.ClipRectangle.Width - sizeChar.Width - drawPad, drawMidY);

                    if (cardData.scoreModifier != 0)
                    {
                        string modStr = Math.Abs(cardData.scoreModifier).ToString();
                        DrawShadowedString(e.Graphics, modStr, drawMidX, drawMidY,
                            (cardData.scoreModifier > 0) ? fontBrushModPlus : fontBrushModMinus);
                    }

                    if (cardData.card.Type != ETriadCardType.None)
                    {
                        int typeIdx = (int)cardData.card.Type - 1;
                        Image typeImage = cardTypes.Images[typeIdx];

                        e.Graphics.DrawImage(typeImage, e.ClipRectangle.Width - cardTypes.ImageSize.Width, 0);
                    }

                    {
                        int rarityIdx = (int)cardData.card.Rarity;
                        Image rarityImage = cardRarity.Images[rarityIdx];

                        e.Graphics.DrawImage(rarityImage, 0, 0, rarityImage.Width * 2 / 3, rarityImage.Height * 2 / 3);
                    }
                }
            }
        }
    }
}
