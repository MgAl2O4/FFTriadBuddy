using System;
using System.ComponentModel;

namespace FFTriadBuddy.UI
{
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

        public PageInfoViewModel()
        {
            var settingsDB = PlayerSettingsDB.Get();

            // avoid setters here
            valueFontSize = settingsDB.fontSize;
            ValueAlwaysOnTop = settingsDB.alwaysOnTop;
            valueMarkerCard = settingsDB.markerDurationCard;
            valueMarkerSwap = settingsDB.markerDurationSwap;
            valueMarkerCactpot = settingsDB.markerDurationCactpot;
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
}
