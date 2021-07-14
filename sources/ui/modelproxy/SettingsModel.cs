using MgAl2O4.GoogleAPI;
using MgAl2O4.Utils;
using System;
using System.Threading.Tasks;

namespace FFTriadBuddy.UI
{
    public class SettingsModel
    {
        public enum CloudSaveState
        {
            None,
            Loaded,
            Saved,
            UpToDate,
        }

        public static GoogleDriveService CloudStorage;
        public static event Action<GoogleDriveService.EState> OnCloudStorageApiUpdate;
        public static event Action<CloudSaveState> OnCloudStorageStateUpdate;

        private static bool cloudSettingsInitialized = false;
        private static bool cloudSettingsCanSave = false;
        private static object cloudSettingsSyncLock = new object();
        private static Task cloudSettingsUpdateTask;

        private static CloudSaveState cachedSaveState;
        private static GoogleDriveService.EState cachedApiState;

        public static void Initialize()
        {
            var settingsDB = PlayerSettingsDB.Get();

            bool loaded = settingsDB.Load();
            if (loaded)
            {
                settingsDB.SaveBackup();
            }
            else
            {
                Logger.WriteLine("Warning: failed to load player settings!");
            }

            if (!string.IsNullOrEmpty(settingsDB.forcedLanguage))
            {
                LocalizationDB.SetCurrentUserLanguage(settingsDB.forcedLanguage);
            }

            if (settingsDB.useXInput)
            {
                XInputStub.StartPolling();
            }

            CloudStorage = new GoogleDriveService(
                GoogleClientIdentifiers.Keys,
                new GoogleOAuth2.Token() { refreshToken = settingsDB.cloudToken });
        }

        public static void Close()
        {
            lock (cloudSettingsSyncLock)
            {
                cloudSettingsCanSave = false;
            }

            var settingsDB = PlayerSettingsDB.Get();
            if (settingsDB.isDirty && settingsDB.useCloudStorage)
            {
                _ = CloudStorageSave();
            }

            settingsDB.Save();
        }

        public static void SetUseCloudSaves(bool useCloud)
        {
            GoogleOAuth2.KillPendingAuthorization();

            var settingsDB = PlayerSettingsDB.Get();
            settingsDB.useCloudStorage = useCloud;

            if (useCloud && CloudStorage != null)
            {
                if (!cloudSettingsInitialized)
                {
                    CloudStorageInit();
                }
                else
                {
                    CloudStorageSendNotifies(CloudSaveState.UpToDate);
                }
            }

            lock (cloudSettingsSyncLock)
            {
                cloudSettingsCanSave = useCloud;
            }

            if (cloudSettingsUpdateTask == null)
            {
                cloudSettingsUpdateTask = new Task(async () =>
                {
                    const int intervalMs = 2 * 60 * 1000;
                    while (true)
                    {
                        await Task.Delay(intervalMs);

                        bool canSave = false;
                        lock (cloudSettingsSyncLock)
                        {
                            canSave = cloudSettingsCanSave;
                        }

                        if (canSave)
                        {
                            if (PlayerSettingsDB.Get().isDirty)
                            {
                                await CloudStorageSave();
                            }
                            else
                            {
                                CloudStorageSendNotifies(CloudSaveState.UpToDate);
                            }
                        }
                    }
                });
                cloudSettingsUpdateTask.Start();
            }
        }

        public static async void CloudStorageInit()
        {
            cachedApiState = GoogleDriveService.EState.AuthInProgress;
            OnCloudStorageApiUpdate.Invoke(cachedApiState);

            try
            {
                await CloudStorage.InitFileList();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Exception: " + ex);
            }

            OnCloudStorageApiUpdate.Invoke(CloudStorage.GetState());

            var settingsDB = PlayerSettingsDB.Get();
            settingsDB.cloudToken = CloudStorage.GetAuthToken().refreshToken;

            bool needsSave = await CloudStorageLoad();
            cloudSettingsInitialized = true;

            if (needsSave)
            {
                await CloudStorageSave();
            }
        }

        private static async Task<bool> CloudStorageLoad()
        {
            string fileContent = null;
            try
            {
                fileContent = await CloudStorage.DownloadTextFile("FFTriadBuddy-settings.json");

                Logger.WriteLine("Loaded cloud save, API response: " + CloudStorage.GetLastApiResponse());
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Exception: " + ex);
            }

            CloudStorageSendNotifies(CloudSaveState.Loaded);

            bool needsSave = true;
            if (!string.IsNullOrEmpty(fileContent))
            {
                needsSave = PlayerSettingsDB.Get().MergeWithContent(fileContent);
            }

            return needsSave;
        }

        private static async Task CloudStorageSave()
        {
            string fileContent = PlayerSettingsDB.Get().SaveToString();
            if (!string.IsNullOrEmpty(fileContent))
            {
                try
                {
                    await CloudStorage.UploadTextFile("FFTriadBuddy-settings.json", fileContent);

                    Logger.WriteLine("Created cloud save, API response: " + CloudStorage.GetLastApiResponse());
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Exception: " + ex);
                }

                CloudStorageSendNotifies(CloudSaveState.Saved);
            }
        }

        private static void CloudStorageSendNotifies(CloudSaveState state)
        {
            cachedApiState = CloudStorage.GetState();
            cachedSaveState = state;
            CloudStorageRequestState();
        }

        public static void CloudStorageRequestState()
        {
            if (cachedApiState == GoogleDriveService.EState.NoErrors)
            {
                OnCloudStorageStateUpdate.Invoke(cachedSaveState);
            }
            else
            {
                OnCloudStorageApiUpdate.Invoke(cachedApiState);
            }
        }
    }
}
