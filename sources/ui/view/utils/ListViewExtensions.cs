using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FFTriadBuddy.UI
{
    public class ListViewExtensions
    {
        public class SortInfo
        {
            public GridViewColumn column;
            public ListSortDirection direction;
        }

        private static readonly PropertyInfo InheritanceContextProp = typeof(DependencyObject).GetProperty("InheritanceContext", BindingFlags.NonPublic | BindingFlags.Instance);


        public static DependencyProperty SortInfoProperty = DependencyProperty.RegisterAttached("SortInfo", typeof(SortInfo), typeof(ListViewExtensions));

        public static SortInfo GetSortInfo(ListView owner)
        {
            return (SortInfo)owner.GetValue(SortInfoProperty);
        }

        public static void SetSortInfo(ListView owner, SortInfo value)
        {
            owner.SetValue(SortInfoProperty, value);
        }


        public static DependencyProperty ColumnSortPropertyProperty = DependencyProperty.RegisterAttached("ColumnSortProperty", typeof(string), typeof(ListViewExtensions));

        public static string GetColumnSortProperty(GridViewColumn owner)
        {
            return (string)owner.GetValue(ColumnSortPropertyProperty);
        }

        public static void SetColumnSortProperty(GridViewColumn owner, string value)
        {
            owner.SetValue(ColumnSortPropertyProperty, value);
        }

        public static DependencyProperty InitSortingProperty = DependencyProperty.RegisterAttached("InitSorting", typeof(string), typeof(ListViewExtensions));

        public static string GetInitSorting(GridViewColumn owner)
        {
            return (string)owner.GetValue(InitSortingProperty);
        }

        public static void SetInitSorting(GridViewColumn owner, string value)
        {
            owner.SetValue(InitSortingProperty, value);
        }

        public static DependencyProperty EnableSortOnClickProperty = DependencyProperty.RegisterAttached("EnableSortOnClick", typeof(bool), typeof(ListViewExtensions),
            new PropertyMetadata(false, new PropertyChangedCallback(EnableSortOnClickChanged)));

        public static bool GetEnableSortOnClick(ListView owner)
        {
            return (bool)owner.GetValue(EnableSortOnClickProperty);
        }

        public static void SetEnableSortOnClick(ListView owner, bool value)
        {
            owner.SetValue(EnableSortOnClickProperty, value);
        }

        public static void EnableSortOnClickChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null || e.NewValue == e.OldValue)
            {
                return;
            }

            bool enableSorting = (bool)e.NewValue;
            if (enableSorting)
            {
                listView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(HandlerColumnClicked));
                if (listView.View == null)
                {
                    listView.Loaded += new RoutedEventHandler(HandlerViewLoaded);
                }
                else
                {
                    InitializeViewSorting(listView);
                }
            }
            else
            {
                listView.RemoveHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(HandlerColumnClicked));
            }
        }

        public static void HandlerViewLoaded(object sender, RoutedEventArgs e)
        {
            var listView = e.Source as ListView;
            InitializeViewSorting(listView);
            listView.Loaded -= new RoutedEventHandler(HandlerViewLoaded);
        }

        public static void InitializeViewSorting(ListView listView)
        {
            GridView gridView = (listView != null) ? (listView.View as GridView) : null;
            if (gridView != null)
            {
                bool hasSorted = false;
                foreach (var column in gridView.Columns)
                {
                    var mode = GetInitSorting(column);
                    if (mode != null)
                    {
                        var sortInfo = new SortInfo() { column = column, direction = (mode == "ASC" ? ListSortDirection.Descending : ListSortDirection.Ascending) };
                        SetSortInfo(listView, sortInfo);

                        ApplyViewSorting(listView, column);
                        hasSorted = true;
                        break;
                    }
                }

                if (!hasSorted)
                {
                    foreach (var column in gridView.Columns)
                    {
                        if (column.DisplayMemberBinding != null)
                        {
                            ApplyViewSorting(listView, column);
                            break;
                        }
                    }
                }
            }
        }

        public static void HandlerColumnClicked(object sender, RoutedEventArgs e)
        {
            var columnHeader = e.OriginalSource as GridViewColumnHeader;
            var column = columnHeader != null ? columnHeader.Column : null;
            var listView = FindObjectParent<ListView>(column);

            if (column != null && listView != null)
            {
                ApplyViewSorting(listView, column);
            }
        }

        public static void ApplyViewSorting(ListView listView, GridViewColumn column)
        {
            if (listView == null && listView.Items == null)
            {
                return;
            }

            SortInfo sortInfo = GetSortInfo(listView);
            if (sortInfo == null)
            {
                sortInfo = new SortInfo();
            }

            if (sortInfo.column != column)
            {
                sortInfo.column = column;
                sortInfo.direction = ListSortDirection.Ascending;
            }
            else
            {
                sortInfo.direction = (sortInfo.direction == ListSortDirection.Ascending) ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }

            var sortProp = (column.DisplayMemberBinding is Binding) ? (column.DisplayMemberBinding as Binding).Path.Path : GetColumnSortProperty(column);
            if (!string.IsNullOrEmpty(sortProp))
            {
                listView.Items.SortDescriptions.Clear();
                listView.Items.SortDescriptions.Add(new SortDescription(sortProp, sortInfo.direction));
            }

            SetSortInfo(listView, sortInfo);
        }

        public static DependencyObject FindObjectParent(DependencyObject source, Predicate<DependencyObject> isTypeMatching)
        {
            DependencyObject testOb = source;
            while (testOb != null && !isTypeMatching(testOb))
            {
                var nextParentOb = LogicalTreeHelper.GetParent(testOb);
                if (nextParentOb == null)
                {
                    if (testOb is FrameworkElement)
                    {
                        nextParentOb = VisualTreeHelper.GetParent(testOb);
                    }

                    if (nextParentOb == null && testOb is ContentElement)
                    {
                        nextParentOb = ContentOperations.GetParent((ContentElement)testOb);
                    }

                    if (nextParentOb == null)
                    {
                        nextParentOb = InheritanceContextProp.GetValue(testOb, null) as DependencyObject;
                    }
                }

                testOb = nextParentOb;
            }

            return testOb;
        }

        public static T FindObjectParent<T>(DependencyObject source) where T : DependencyObject
        {
            return FindObjectParent(source, x => x is T) as T;
        }

        public static DependencyProperty SyncScrollItemProperty = DependencyProperty.RegisterAttached("SyncScrollItem", typeof(object), typeof(ListViewExtensions),
            new PropertyMetadata(null, new PropertyChangedCallback(SyncScrollItemChanged)));

        public static object GetSyncScrollItem(ListView owner)
        {
            return (object)owner.GetValue(SyncScrollItemProperty);
        }

        public static void SetSyncScrollItem(ListView owner, object value)
        {
            owner.SetValue(SyncScrollItemProperty, value);
        }

        public static void SyncScrollItemChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null || e.NewValue == e.OldValue)
            {
                return;
            }

            listView.SelectedItem = e.NewValue;
            listView.ScrollIntoView(e.NewValue);
        }
    }
}
