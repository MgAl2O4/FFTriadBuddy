using System;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public class TabControlNoTabs : TabControl
    {
        public static bool bIsRoutingCreateMesage = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x1328)// && !DesignMode)
            {
                m.Result = (IntPtr)1;
            }
            else if (m.Msg == 0x1)
            {
                // mark sending create messages, it can get spammy on nested list views that are trying to process all initial OnChecked events
                bIsRoutingCreateMesage = true;
                base.WndProc(ref m);
                bIsRoutingCreateMesage = false;
            }
            else
            {
                base.WndProc(ref m);
            }
        }
    }
}
