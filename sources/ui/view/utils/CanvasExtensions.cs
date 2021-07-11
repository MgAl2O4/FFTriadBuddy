using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FFTriadBuddy.UI
{
    public class CanvasExtensions
    {
        public static DependencyProperty AutoHideProperty = DependencyProperty.RegisterAttached("AutoHide", typeof(bool), typeof(CanvasExtensions),
            new PropertyMetadata(false, new PropertyChangedCallback(OnAutoHideChanged)));

        public static bool GetAutoHide(FrameworkElement owner)
        {
            return (bool)owner.GetValue(AutoHideProperty);
        }

        public static void SetAutoHide(FrameworkElement owner, bool value)
        {
            owner.SetValue(AutoHideProperty, value);
        }

        public static void OnAutoHideChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var owner = sender as FrameworkElement;
            if (owner != null && !owner.IsLoaded)
            {
                bool designMode = DesignerProperties.GetIsInDesignMode(sender);
                if (!designMode)
                {
                    owner.Visibility = Visibility.Hidden;
                }
            }
        }

        public static DependencyProperty ScreenBoundsProperty = DependencyProperty.RegisterAttached("ScreenBounds", typeof(OverlayWindowViewModel.ScreenCoordVM), typeof(CanvasExtensions),
            new PropertyMetadata(null, new PropertyChangedCallback(OnScreenBoundsChanged)));

        public static OverlayWindowViewModel.ScreenCoordVM GetScreenBounds(FrameworkElement owner)
        {
            return (OverlayWindowViewModel.ScreenCoordVM)owner.GetValue(ScreenBoundsProperty);
        }

        public static void SetScreenBounds(FrameworkElement owner, OverlayWindowViewModel.ScreenCoordVM value)
        {
            owner.SetValue(ScreenBoundsProperty, value);
        }

        public static void OnScreenBoundsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var owner = sender as FrameworkElement;
            if (owner != null)
            {
                if (!owner.IsLoaded)
                {
                    owner.Loaded += UpdateScreenBoundsOnLoaded;
                }
                else
                {
                    UpdateScreenBounds(owner, e.NewValue as OverlayWindowViewModel.ScreenCoordVM);
                }
            }
        }

        private static void UpdateScreenBoundsOnLoaded(object sender, RoutedEventArgs e)
        {
            var owner = sender as FrameworkElement;
            owner.Loaded -= UpdateScreenBoundsOnLoaded;

            var bounds = GetScreenBounds(owner);
            UpdateScreenBounds(owner, bounds);
        }

        private static Dictionary<object, DispatcherTimer> mapTimers = new Dictionary<object, DispatcherTimer>();
        private static void UpdateScreenBounds(FrameworkElement owner, OverlayWindowViewModel.ScreenCoordVM screenCoordVM)
        {
            if (mapTimers.TryGetValue(owner, out var existingTimer))
            {
                if (existingTimer.IsEnabled)
                {
                    existingTimer.Stop();
                }

                mapTimers.Remove(owner);
            }

            if (screenCoordVM != null)
            {
                var canvasOwner = ViewUtils.FindVisualParent(owner, x => x is Canvas) as Canvas;
                var overlayOwner = ViewUtils.FindVisualParent(owner, x => x is OverlayWindowInteractive) as OverlayWindowInteractive;
                var localPos = canvasOwner.PointFromScreen(new Point(screenCoordVM.ScreenCoords.X, screenCoordVM.ScreenCoords.Y));

                switch (screenCoordVM.DrawMode)
                {
                    case OverlayWindowViewModel.ScreenCoordVM.Mode.CapturePanel:
                        // coord only, anchor: center + bottom (with margin)
                        if (overlayOwner != null)
                        {
                            overlayOwner.SetPanelCanvasPos(localPos.X - (owner.ActualWidth / 2), localPos.Y - owner.ActualHeight - 10);
                        }
                        else
                        {
                            Canvas.SetLeft(owner, localPos.X - (owner.ActualWidth / 2));
                            Canvas.SetTop(owner, localPos.Y - owner.ActualHeight - 10);
                        }
                        break;

                    case OverlayWindowViewModel.ScreenCoordVM.Mode.AdjustedCapturePanel:
                        // coord only, anchor: center + top (with margin + coord.height)
                        if (overlayOwner != null)
                        {
                            overlayOwner.SetPanelCanvasPos(localPos.X + ((screenCoordVM.ScreenSize.Width - owner.ActualWidth) / 2), localPos.Y + screenCoordVM.ScreenSize.Height + 50);
                        }
                        else
                        {
                            Canvas.SetLeft(owner, localPos.X + ((screenCoordVM.ScreenSize.Width - owner.ActualWidth) / 2));
                            Canvas.SetTop(owner, localPos.Y + screenCoordVM.ScreenSize.Height + 50);
                        }
                        break;

                    case OverlayWindowViewModel.ScreenCoordVM.Mode.SwapWarning:
                        // coord only, anchor: left + bottom (with margin)
                        Canvas.SetLeft(owner, localPos.X);
                        Canvas.SetTop(owner, localPos.Y - owner.ActualHeight - 10);
                        break;

                    default:
                        Canvas.SetLeft(owner, localPos.X);
                        Canvas.SetTop(owner, localPos.Y);
                        owner.Width = screenCoordVM.ScreenSize.Width;
                        owner.Height = screenCoordVM.ScreenSize.Height;
                        break;
                }

                owner.Visibility = Visibility.Visible;
                if (screenCoordVM.Duration > 0)
                {
                    var timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(screenCoordVM.Duration) };
                    timer.Tick += (s, e) =>
                    {
                        owner.Visibility = Visibility.Hidden;
                        mapTimers.Remove(owner);
                        ((DispatcherTimer)s).Stop();
                    };

                    timer.Start();
                    mapTimers.Add(owner, timer);
                }
            }
            else
            {
                owner.Visibility = Visibility.Hidden;
            }
        }
    }
}
