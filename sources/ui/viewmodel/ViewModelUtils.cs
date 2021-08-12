using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected void PropertySetAndNotify<T>(T value, ref T property, [CallerMemberName] string name = null)
        {
            property = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public abstract class LocalizedViewModel : BaseViewModel
    {
        public virtual void RefreshLocalization()
        {
            var allProps = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in allProps)
            {
                if (prop.PropertyType == typeof(string))
                {
                    OnPropertyChanged(prop.Name);
                }
            }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private Action<T> execute = null;
        private Predicate<T> canExecute = null;

        public RelayCommand(Action<T> execute)
        {
            this.execute = execute;
            this.canExecute = null;
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public bool CanExecute(object parameter)
        {
            return (canExecute != null) ? canExecute.Invoke((T)parameter) : true;
        }

        public void Execute(object parameter)
        {
            execute.Invoke((T)parameter);
        }
    }

    public interface IDialogWindowViewModel
    {
        string GetDialogWindowTitle();

        event Action<bool?> RequestDialogWindowClose;
    }

    public interface IOverlayWindowViewModel
    {
    }

    public interface IDialogWindowService
    {
        bool? ShowDialog(IDialogWindowViewModel viewModel);
    }

    public interface IOverlayWindowService
    {
        void SetOverlayActive(IOverlayWindowViewModel viewModel, bool wantsActive);
        bool IsCursorInside(Rectangle screenBounds);
        void OnProcessingGameWindow(Rectangle gameWindowBounds, out bool invalidatedPosition);
        Rectangle GetScreenBounds(Rectangle gameWindowBounds);
    }

    public interface IAppWindowService
    {
        void SetFontSize(float value);
        void SetAlwaysOnTop(bool value);
        void SetSoftwareRendering(bool value);
    }

    public class ViewModelServices
    {
        public static IDialogWindowService DialogWindow;
        public static IOverlayWindowService OverlayWindow;
        public static IAppWindowService AppWindow;
    }
}
