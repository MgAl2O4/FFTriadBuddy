using System;
using System.ComponentModel;
using System.Windows;

namespace FFTriadBuddy.UI
{
    public class SettingsEventArgs : EventArgs
    {
        public enum Setting
        {
            UseSmallIcons,
        }

        public Setting Type;
        public bool BoolValue;
        public float FloatValue;
    }

    public class PageInfoViewModel : LocalizedViewModel, IDataErrorInfo
    {
        public MainWindowViewModel MainWindow;
        public LocalSavesViewModel LocalSaves { get; } = new LocalSavesViewModel();

        private float valueFontSize = 0;
        public float ValueFontSize
        {
            get => valueFontSize;
            set
            {
                PropertySetAndNotify(value, ref valueFontSize);
                if (IsPropertyValueValid(value, GetValidFontSizeRange))
                {
                    PlayerSettingsDB.Get().fontSize = valueFontSize;
                    ViewModelServices.AppWindow.SetFontSize(valueFontSize);
                }
            }
        }

        private float valueMarkerCard = 0;
        public float ValueMarkerCard
        {
            get => valueMarkerCard;
            set
            {
                PropertySetAndNotify(value, ref valueMarkerCard);
                if (IsPropertyValueValid(value, GetValidMarkerDurationRange))
                {
                    PlayerSettingsDB.Get().markerDurationCard = valueMarkerCard;
                }
            }
        }

        private float valueMarkerSwap = 0;
        public float ValueMarkerSwap
        {
            get => valueMarkerSwap;
            set
            {
                PropertySetAndNotify(value, ref valueMarkerSwap);
                if (IsPropertyValueValid(value, GetValidMarkerDurationRange))
                {
                    PlayerSettingsDB.Get().markerDurationSwap = valueMarkerSwap;
                }
            }
        }

        private float valueMarkerCactpot = 0;
        public float ValueMarkerCactpot
        {
            get => valueMarkerCactpot;
            set
            {
                PropertySetAndNotify(value, ref valueMarkerCactpot);
                if (IsPropertyValueValid(value, GetValidMarkerDurationRange))
                {
                    PlayerSettingsDB.Get().markerDurationCactpot = valueMarkerCactpot;
                }
            }
        }

        private bool valueAlwaysOnTop = false;
        public bool ValueAlwaysOnTop
        {
            get => valueAlwaysOnTop;
            set
            {
                PropertySetAndNotify(value, ref valueAlwaysOnTop);
                PlayerSettingsDB.Get().alwaysOnTop = valueAlwaysOnTop;
                ViewModelServices.AppWindow.SetAlwaysOnTop(valueAlwaysOnTop);
            }
        }

        private bool valueSkipOptionalRules = false;
        public bool ValueSkipOptionalRules
        {
            get => valueSkipOptionalRules;
            set
            {
                PropertySetAndNotify(value, ref valueSkipOptionalRules);
                PlayerSettingsDB.Get().skipOptionalSimulateRules = valueSkipOptionalRules;
            }
        }

        private bool valueUseSmallIcons = false;
        public bool ValueUseSmallIcons
        {
            get => valueUseSmallIcons;
            set
            {
                PropertySetAndNotify(value, ref valueUseSmallIcons);
                PlayerSettingsDB.Get().useSmallIcons = valueUseSmallIcons;

                OnSettingsChanged?.Invoke(this, new SettingsEventArgs() { Type = SettingsEventArgs.Setting.UseSmallIcons, BoolValue = value });
            }
        }

        private bool valueDisableHardwareAcceleration = false;
        public bool ValueDisableHardwareAcceleration
        {
            get => valueDisableHardwareAcceleration;
            set
            {
                PropertySetAndNotify(value, ref valueDisableHardwareAcceleration);
                PlayerSettingsDB.Get().useSoftwareRendering = valueDisableHardwareAcceleration;

                ViewModelServices.AppWindow.SetSoftwareRendering(valueDisableHardwareAcceleration);
            }
        }

        public string MainForm_Info_HomePage => loc.strings.MainForm_Info_HomePage;
        public string MainForm_Info_BugReports => loc.strings.MainForm_Info_BugReports;
        public string MainForm_Info_Localization => loc.strings.MainForm_Info_Localization;
        public string MainForm_Info_TranslatorLove => loc.strings.MainForm_Info_TranslatorLove;
        public string MainForm_Info_TranslatorNeeded => loc.strings.MainForm_Info_TranslatorNeeded;

        public string Settings_AlwaysOnTop => loc.strings.Settings_AlwaysOnTop;
        public string Settings_Title => loc.strings.Settings_Title;
        public string Settings_FontSize => loc.strings.Settings_FontSize;
        public string Settings_MarkerDurationCard => loc.strings.Settings_MarkerDurationCard;
        public string Settings_MarkerDurationSwap => loc.strings.Settings_MarkerDurationSwap;
        public string Settings_MarkerDurationCactpot => loc.strings.Settings_MarkerDurationCactpot;
        public string Settings_SkipOptionalSimulateRules => loc.strings.Settings_SkipOptionalSimulateRules;
        public string Settings_AlwaysSmallIcons => loc.strings.Settings_AlwaysSmallIcons;
        public string Settings_DisableHardwareAcceleration => loc.strings.Settings_DisableHardwareAcceleration;

        public string Error => null;
        public string this[string columnName]
        {
            get
            {
                if (columnName == "ValueFontSize")
                {
                    var (minV, maxV) = GetValidFontSizeRange();
                    if (valueFontSize < minV || valueFontSize > maxV)
                    {
                        return string.Format("[{0} .. {1}]", minV, maxV);
                    }
                }
                else if (columnName == "ValueMarkerCard" || columnName == "ValueMarkerSwap" || columnName == "ValueMarkerCactpot")
                {
                    float testV =
                        (columnName == "ValueMarkerCard") ? valueMarkerCard :
                        (columnName == "ValueMarkerSwap") ? valueMarkerSwap :
                        valueMarkerCactpot;

                    var (minV, maxV) = GetValidMarkerDurationRange();
                    if (testV < minV || testV > maxV)
                    {
                        return string.Format("[{0} .. {1}]", minV, maxV);
                    }
                }

                return null;
            }
        }

        public event EventHandler<SettingsEventArgs> OnSettingsChanged;
        public static PageInfoViewModel lastInstance;

        public PageInfoViewModel()
        {
            var settingsDB = PlayerSettingsDB.Get();
            lastInstance = this;

            ValueAlwaysOnTop = settingsDB.alwaysOnTop;
            ValueSkipOptionalRules = settingsDB.skipOptionalSimulateRules;

            // avoid setters here
            valueFontSize = settingsDB.fontSize;
            valueMarkerCard = settingsDB.markerDurationCard;
            valueMarkerSwap = settingsDB.markerDurationSwap;
            valueMarkerCactpot = settingsDB.markerDurationCactpot;
            valueUseSmallIcons = settingsDB.useSmallIcons;
            valueDisableHardwareAcceleration = settingsDB.useSoftwareRendering;
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            LocalSaves.RefreshLocalization();
        }

        private (float, float) GetValidFontSizeRange()
        {
            return (10.0f, 40.0f);
        }

        private (float, float) GetValidMarkerDurationRange()
        {
            return (0.5f, 10.0f);
        }

        private bool IsPropertyValueValid(float value, Func<(float, float)> funcRange)
        {
            var (minV, maxV) = funcRange();
            return (value >= minV) && (value <= maxV);
        }
    }

    public class SettingsWeakEventManager : WeakEventManager
    {
        private SettingsWeakEventManager() { }

        public static void AddHandler(PageInfoViewModel sourceVM, EventHandler<SettingsEventArgs> handler)
        {
            if (sourceVM != null && handler != null)
            {
                CurrentManager.ProtectedAddHandler(sourceVM, handler);
            }
        }

        public static void RemoveHandler(PageInfoViewModel sourceVM, EventHandler<SettingsEventArgs> handler)
        {
            if (sourceVM != null && handler != null)
            {
                CurrentManager.ProtectedRemoveHandler(sourceVM, handler);
            }
        }

        private static SettingsWeakEventManager CurrentManager
        {
            get
            {
                Type managerType = typeof(SettingsWeakEventManager);
                SettingsWeakEventManager manager =
                    (SettingsWeakEventManager)GetCurrentManager(managerType);

                // at first use, create and register a new manager
                if (manager == null)
                {
                    manager = new SettingsWeakEventManager();
                    SetCurrentManager(managerType, manager);
                }

                return manager;
            }
        }

        protected override ListenerList NewListenerList()
        {
            return new ListenerList<SettingsEventArgs>();
        }

        protected override void StartListening(object source)
        {
            PageInfoViewModel typedSource = (PageInfoViewModel)source;
            typedSource.OnSettingsChanged += new EventHandler<SettingsEventArgs>(OnSettingsChanged);
        }

        protected override void StopListening(object source)
        {
            PageInfoViewModel typedSource = (PageInfoViewModel)source;
            typedSource.OnSettingsChanged -= new EventHandler<SettingsEventArgs>(OnSettingsChanged);
        }

        void OnSettingsChanged(object sender, SettingsEventArgs e)
        {
            DeliverEvent(sender, e);
        }
    }
}
