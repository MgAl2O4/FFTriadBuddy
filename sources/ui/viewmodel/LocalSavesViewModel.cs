using MgAl2O4.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class LocalSavesViewModel : LocalizedViewModel
    {
        public string LocalSaves_Export => loc.strings.LocalSaves_Export;
        public string LocalSaves_Import => loc.strings.LocalSaves_Import;
        public string LocalSaves_ShowBackupFolder => loc.strings.LocalSaves_ShowBackupFolder;
        public string LocalSaves_Title => loc.strings.LocalSaves_Title;

        public ICommand CommandExport { get; private set; }
        public ICommand CommandImport { get; private set; }
        public ICommand CommandViewBackups { get; private set; }

        private readonly string defaultPath;

        public LocalSavesViewModel()
        {
            CommandExport = new RelayCommand<object>(CommandExportFunc);
            CommandImport = new RelayCommand<object>(CommandImportFunc);
            CommandViewBackups = new RelayCommand<object>(CommandViewBackupFunc);

            defaultPath = PlayerSettingsDB.Get().GetBackupFolderPath();
        }

        private void CommandExportFunc(object dummyParam)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "export",
                DefaultExt = ".json",
                Filter = "Settings|*.json",
                InitialDirectory = defaultPath,
                OverwritePrompt = true
            };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                try
                {
                    var fileName = dialog.FileName;
                    Logger.WriteLine("Exporting settings to: {0}", fileName);

                    string jsonStr = PlayerSettingsDB.Get().SaveToJson(true);
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }

                    File.WriteAllText(fileName, jsonStr);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Failed to export: {0}", ex);
                }
            }
        }

        private void CommandImportFunc(object dummyParam)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                FileName = "export",
                DefaultExt = ".json",
                Filter = "Settings|*.json",
                InitialDirectory = defaultPath,
                CheckFileExists = true
            };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                try
                {
                    var fileName = dialog.FileName;
                    Logger.WriteLine("Importing settings from: {0}", fileName);

                    string jsonStr = File.ReadAllText(fileName);

                    var settingsDB = PlayerSettingsDB.Get();
                    settingsDB.LoadFromJson(jsonStr);
                    settingsDB.OnImport();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Failed to import: {0}", ex);
                }
            }
        }

        private void CommandViewBackupFunc(object dummyParam)
        {
            Process.Start(defaultPath);
        }
    }
}
