using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFTriadBuddy.UI
{
    public class ViewUtils
    {
        public static DependencyObject FindVisualChildRecursive(DependencyObject parentOb, Func<DependencyObject, bool> predicate)
        {
            int numChildren = parentOb != null ? VisualTreeHelper.GetChildrenCount(parentOb) : 0;
            for (int idx = 0; idx < numChildren; idx++)
            {
                var childOb = VisualTreeHelper.GetChild(parentOb, idx);
                if (predicate(childOb))
                {
                    return childOb;
                }

                var foundMatch = FindVisualChildRecursive(childOb, predicate);
                if (foundMatch != null)
                {
                    return foundMatch;
                }
            }

            return null;
        }

        public static DependencyObject FindVisualParent(DependencyObject testOb, Func<DependencyObject, bool> predicate)
        {
            while (testOb != null)
            {
                if (predicate(testOb))
                {
                    return testOb;
                }

                testOb = VisualTreeHelper.GetParent(testOb);
            }

            return null;
        }
    }

    public class NoAutoWidthDecorator : Decorator
    {
        protected override Size MeasureOverride(Size constraint)
        {
            Child.Measure(constraint);
            return new Size(0, Child.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var childElem = Child as FrameworkElement;
            childElem.MaxWidth = arrangeSize.Width;
            childElem.MinHeight = arrangeSize.Height;

            return base.ArrangeOverride(arrangeSize);
        }
    }
}
