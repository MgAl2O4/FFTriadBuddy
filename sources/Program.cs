using MgAl2O4.Utils;
using System;
using System.Linq;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] Args)
        {
            Logger.Initialize(Args);

            bool bUpdatePending = GithubUpdater.FindAndApplyUpdates();
            if (bUpdatePending)
            {
                return;
            }

            bool bInit = Form1.InitializeGameAssets();
            if (bInit)
            {
                if (Args.Contains("-dataConvert"))
                {
                    DataConverter converter = new DataConverter();
                    converter.Run();
                }
                else if (Args.Contains("-solverStress"))
                {
                    TriadGameSession.RunSolverStressTest();
                }
                else
                {
#if DEBUG
                    if (Args.Contains("-runTests"))
                    {
                        TestManager.RunTests();
                    }
#endif // DEBUG

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }
            }
            else
            {
                MessageBox.Show("Failed to initialize resources!", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }

            Logger.Close();
        }
    }
}
