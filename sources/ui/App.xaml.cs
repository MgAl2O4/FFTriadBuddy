using MgAl2O4.Utils;
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool canSaveSettings = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Logger.Initialize(e.Args);

            bool canStart = false;

            bool updatePending = GithubUpdater.FindAndApplyUpdates();
            if (!updatePending)
            {
                bool hasAssets = LoadAssets();
                if (hasAssets)
                {
                    canStart = true;
                }
                else
                {
                    string appName = Assembly.GetEntryAssembly().GetName().Name;
                    MessageBox.Show("Failed to initialize resources!", appName, MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            }

#if DEBUG
            if (Array.Find(e.Args, x => x == "-runTests") != null)
            {
                TestManager.RunTests();
                canStart = false;
            }

            if (Array.Find(e.Args, x => x == "-dataConvert") != null)
            {
                var converter = new DataConverter();
                converter.Run();
                canStart = false;
            }

            if (Array.Find(e.Args, x => x == "-runSolverAccTest") != null)
            {
                TriadGameTests.RunSolverAccuracyTests();
                canStart = false;
            }
            else if (Array.Find(e.Args, x => x == "-runSolverStressTest") != null)
            {
                TriadGameTests.RunSolverStressTest();
                canStart = false;
            }
            else if (Array.Find(e.Args, x => x == "-generateSolverTrainingData") != null)
            {
                TriadGameTests.GenerateAccuracyTrainingData();
                canStart = false;
            }
#endif // DEBUG

            if (canStart)
            {
                int renderingTier = RenderCapability.Tier >> 16;
                Logger.WriteLine("Rendering tier:{0}", renderingTier);

                DialogWindowService.Initialize();
                OverlayWindowService.Initialize();
                AppWindowService.Initialize();

                canSaveSettings = true;

                var settingsDB = PlayerSettingsDB.Get();
                ViewModelServices.AppWindow.SetSoftwareRendering(settingsDB.useSoftwareRendering);

                var window = new MainWindow();
                window.FontSize = settingsDB.fontSize;
                window.Topmost = settingsDB.alwaysOnTop;

                if (settingsDB.lastHeight > window.MinHeight) { window.Height = settingsDB.lastHeight; }
                if (settingsDB.lastWidth > window.MinWidth) { window.Width = settingsDB.lastWidth; }

                window.Show();
            }
            else
            {
                Shutdown();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (canSaveSettings)
            {
                SettingsModel.Close();
            }

            XInputStub.StopPolling();
            Logger.Close();
        }

        private bool LoadAssets()
        {
            bool bResult = false;

            try
            {
                var resManager = new ResourceManager("FFTriadBuddy.Properties.Resources", Assembly.GetExecutingAssembly());
                var assets = (byte[])resManager.GetObject("assets");

                if (AssetManager.Get().Init(assets))
                {
                    LocalizationDB.SetCurrentUserLanguage(CultureInfo.CurrentCulture.Name);

                    bResult = TriadCardDB.Get().Load();
                    bResult = bResult && TriadNpcDB.Get().Load();
                    bResult = bResult && ImageHashDB.Get().Load();
                    bResult = bResult && TriadTournamentDB.Get().Load();
                    bResult = bResult && LocalizationDB.Get().Load();

                    if (bResult)
                    {
                        SettingsModel.Initialize();
                        IconDB.Get().Load();
                        ModelProxyDB.Get().Load();

                        TriadGameSimulation.StaticInitialize();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Init failed: " + ex);
                bResult = false;
            }

            return bResult;
        }
    }
}
