using System;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public class HitInvisConst
    {
        public static readonly int WM_NCHITTEST = 0x84;
        public static readonly int HTTRANSPARENT = -1;
    }

    public class HitInvisibleLabel : Label
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HitInvisConst.WM_NCHITTEST && !DesignMode)
                m.Result = (IntPtr)HitInvisConst.HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }
    }

    public class HitInvisiblePanel : Panel
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HitInvisConst.WM_NCHITTEST && !DesignMode)
                m.Result = (IntPtr)HitInvisConst.HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }
    }
}
