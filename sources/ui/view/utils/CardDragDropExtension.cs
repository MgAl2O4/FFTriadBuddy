using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public interface ICardDragDropSource
    {
        void OnCardDrop(CardViewModel sourceCard, object destContainer);
    }

    public interface ICardDragDropTarget
    {
        bool IsCardDropAllowed(CardViewModel sourceCard, object sourceContainer);
        void OnCardDragEnter(CardViewModel sourceCard, CardViewModel destCard);
        void OnCardDragLeave(CardViewModel sourceCard, CardViewModel destCard);
        void OnCardDrop(CardViewModel sourceCard, CardViewModel destCard, object sourceContainer);
    }

    public class CardDragDropExtension
    {
        private class CardDragData
        {
            public CardViewModel srcCard;
            public object srcControl;
            public object srcContainerOb;
        }

        private static Point mouseDownPos;
        private static Control mouseDownCtrl;
        private static bool isDropAllowed;

        public static readonly DependencyProperty IsDragSourceProperty =
            DependencyProperty.RegisterAttached("IsDragSource", typeof(bool), typeof(CardDragDropExtension), new PropertyMetadata(false, IsDragSourceChanged));

        public static readonly DependencyProperty IsDragDestinationProperty =
            DependencyProperty.RegisterAttached("IsDragDestination", typeof(bool), typeof(CardDragDropExtension), new PropertyMetadata(false, IsDragDestinationChanged));

        public static readonly DependencyProperty CardContextProperty =
            DependencyProperty.RegisterAttached("CardContext", typeof(CardViewModel), typeof(CardDragDropExtension));

        public static readonly DependencyProperty CardContainerProperty =
            DependencyProperty.RegisterAttached("CardContainer", typeof(object), typeof(CardDragDropExtension));

        public static bool GetIsDragSource(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragSourceProperty);
        }

        public static void SetIsDragSource(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragSourceProperty, value);
        }

        public static bool GetIsDragDestination(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragDestinationProperty);
        }

        public static void SetIsDragDestination(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragDestinationProperty, value);
        }

        public static CardViewModel GetCardContext(DependencyObject obj)
        {
            return (CardViewModel)obj.GetValue(CardContextProperty);
        }

        public static void SetCardContext(DependencyObject obj, CardViewModel value)
        {
            obj.SetValue(CardContextProperty, value);
        }

        public static object GetCardContainer(DependencyObject obj)
        {
            return obj.GetValue(CardContainerProperty);
        }

        public static void SetCardContainer(DependencyObject obj, object value)
        {
            obj.SetValue(CardContainerProperty, value);
        }

        private static void IsDragSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var control = obj as Control;
            if (control != null && e.NewValue != e.OldValue)
            {
                if ((bool)e.NewValue)
                {
                    control.PreviewMouseMove += CardDrag_MouseMove;
                    control.PreviewMouseLeftButtonDown += CardDrag_LeftButtonDown;
                    control.PreviewMouseLeftButtonUp += CardDrag_LeftButtonUp;
                }
                else
                {
                    control.PreviewMouseMove -= CardDrag_MouseMove;
                    control.PreviewMouseLeftButtonDown -= CardDrag_LeftButtonDown;
                    control.PreviewMouseLeftButtonUp += CardDrag_LeftButtonUp;
                }
            }
        }

        public static void CardDragReset()
        {
            mouseDownCtrl = null;
        }

        private static void CardDrag_LeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mouseDownPos = e.GetPosition(null);
            mouseDownCtrl = sender as Control;
        }

        private static void CardDrag_LeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mouseDownCtrl = null;
        }

        private static void CardDrag_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && mouseDownCtrl != null && mouseDownCtrl.IsEnabled)
            {
                var pos = e.GetPosition(null);
                bool canStartDrag =
                    Math.Abs(pos.X - mouseDownPos.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(pos.Y - mouseDownPos.Y) >= SystemParameters.MinimumHorizontalDragDistance;

                if (canStartDrag)
                {
                    var cardVM = GetCardContext(mouseDownCtrl);
                    if (cardVM != null)
                    {
                        CardDragData payload = new CardDragData()
                        {
                            srcCard = cardVM,
                            srcContainerOb = GetCardContainer(mouseDownCtrl),
                            srcControl = mouseDownCtrl
                        };

                        DataObject dragData = new DataObject();
                        dragData.SetData(payload);

                        DragDrop.DoDragDrop(mouseDownCtrl, dragData, DragDropEffects.Move);
                    }

                    mouseDownCtrl = null;
                }
            }
        }

        private static void IsDragDestinationChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var control = obj as UIElement;
            if (control != null && e.NewValue != e.OldValue)
            {
                if ((bool)e.NewValue)
                {
                    control.AllowDrop = true;
                    control.PreviewDragEnter += CardDrag_DragEnter;
                    control.PreviewDragLeave += CardDrag_DragLeave;
                    control.PreviewDragOver += CardDrag_DragOver;
                    control.PreviewDrop += CardDrag_Drop;
                }
                else
                {
                    control.AllowDrop = false;
                    control.PreviewDragEnter -= CardDrag_DragEnter;
                    control.PreviewDragLeave -= CardDrag_DragLeave;
                    control.PreviewDragOver -= CardDrag_DragOver;
                    control.PreviewDrop -= CardDrag_Drop;
                }
            }
        }

        private static void CardDrag_Drop(object sender, DragEventArgs e)
        {
            var control = sender as UIElement;
            if (control != null && e.Data.GetDataPresent(typeof(CardDragData)))
            {
                var dragPayload = (CardDragData)e.Data.GetData(typeof(CardDragData));
                var targetContainerOb = GetCardContainer(control);
                var targetContainer = targetContainerOb as ICardDragDropTarget;

                if (targetContainer != null && targetContainer.IsCardDropAllowed(dragPayload.srcCard, dragPayload.srcContainerOb))
                {
                    targetContainer.OnCardDrop(dragPayload.srcCard, GetCardContext(control), dragPayload.srcContainerOb);

                    var srcContainer = dragPayload.srcContainerOb as ICardDragDropSource;
                    if (srcContainer != null)
                    {
                        srcContainer.OnCardDrop(dragPayload.srcCard, targetContainerOb);
                    }
                }
            }
        }

        private static void CardDrag_DragLeave(object sender, DragEventArgs e)
        {
            if (!isDropAllowed) { return; }

            var control = sender as UIElement;
            if (control != null && e.Data.GetDataPresent(typeof(CardDragData)))
            {
                var dragPayload = (CardDragData)e.Data.GetData(typeof(CardDragData));
                var targetContainerOb = GetCardContainer(control);
                var targetContainer = targetContainerOb as ICardDragDropTarget;

                if (targetContainer != null)
                {
                    targetContainer.OnCardDragLeave(dragPayload.srcCard, GetCardContext(control));
                }

                e.Handled = true;
            }
        }

        private static void CardDrag_DragEnter(object sender, DragEventArgs e)
        {
            isDropAllowed = false;

            var control = sender as UIElement;
            if (control != null && e.Data.GetDataPresent(typeof(CardDragData)))
            {
                var dragPayload = (CardDragData)e.Data.GetData(typeof(CardDragData));
                if (dragPayload.srcControl != control)
                {
                    var targetContainerOb = GetCardContainer(control);
                    var targetContainer = targetContainerOb as ICardDragDropTarget;

                    if (targetContainer != null && targetContainer.IsCardDropAllowed(dragPayload.srcCard, dragPayload.srcContainerOb))
                    {
                        targetContainer.OnCardDragEnter(dragPayload.srcCard, GetCardContext(control));
                        isDropAllowed = true;
                    }
                }

                e.Effects = isDropAllowed ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            }
        }

        private static void CardDrag_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = isDropAllowed ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }
    }
}
