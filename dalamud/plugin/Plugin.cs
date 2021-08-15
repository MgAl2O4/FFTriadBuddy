using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;

namespace TriadBuddyPlugin
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Triad Buddy";

        private const string commandName = "/triadbuddy";

        private DalamudPluginInterface pluginInterface;
        private PluginUI pluginUI;
        private GameUI gameUI;
        private GameDataLoader dataLoader;

        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            FFTriadBuddy.TriadGameSession.StaticInitialize();

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(pluginInterface);

            this.pluginInterface = pluginInterface;
            gameUI = new GameUI(pluginInterface);

            // you might normally want to embed resources and load them from the manifest stream
            pluginUI = new PluginUI(gameUI);

            this.pluginInterface.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Shows state of plugin's data"
            });

            pluginInterface.UiBuilder.OnBuildUi += DrawUI;
            pluginInterface.Framework.OnUpdateEvent += OnUpdateState;
            pluginUI.Visible = true;
        }

        private void OnUpdateState(Dalamud.Game.Internal.Framework framework)
        {
            try
            {
                // TODO: async? run every X ms? - check low spec perf, seems to be negligible
                if (dataLoader.IsDataReady)
                {
                    gameUI.Update();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }

        public void Dispose()
        {
            pluginUI.Dispose();

            pluginInterface.Framework.OnUpdateEvent -= OnUpdateState;
            pluginInterface.CommandManager.RemoveHandler(commandName);
            pluginInterface.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            // leaving for now, probably will end up displaying database stats
        }

        private void DrawUI()
        {
            pluginUI.Draw();
        }
    }
}
