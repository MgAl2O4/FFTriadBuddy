using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    // Modified class from:
    // https://stackoverflow.com/a/58066259

    public partial class SearchableComboBox : ComboBox
    {
        public event Action<object, object> SelectionEffectivelyChanged;

        private object effectivelySelectedItem = null;
        public object EffectivelySelectedItem
        {
            get { return effectivelySelectedItem; }
            set { effectivelySelectedItem = value; }
        }

        public SortDescription sorter = new SortDescription();

        private TextBox EditableTextBox => GetTemplateChild("PART_EditableTextBox") as TextBox;
        private bool isTextBoxFreezed = false;
        private bool lockFinalSelection = false;
        private string currentFilter = "";
        private UserChange<bool> IsDropDownOpenUC;

        private Predicate<object> orgViewFilter = null;

        public static readonly DependencyProperty ItemDescEvaluatorProperty = DependencyProperty.RegisterAttached("ItemDescEvaluator", typeof(string), typeof(SearchableComboBox));
        public string ItemDescEvaluator => GetValue(ItemDescEvaluatorProperty).ToString();

        public SearchableComboBox()
        {
            InitializeComponent();

            IsDropDownOpenUC = new UserChange<bool>(v => IsDropDownOpen = v);
            DropDownOpened += FilteredComboBox_DropDownOpened;

            IsEditable = true;
            IsTextSearchEnabled = true;
            StaysOpenOnEdit = true;
            IsReadOnly = false;

            Loaded += (s, e) =>
            {
                if (EditableTextBox != null)
                {
                    new TextBoxBaseUserChangeTracker(EditableTextBox).UserTextChanged += FilteredComboBox_UserTextChange;
                }
            };

            SelectionChanged += (_, __) =>
            {
                shouldTriggerSelectedItemChanged = true;
                if (!isTextBoxFreezed && !lockFinalSelection)
                {
                    TriggerSelectedItemChanged();
                }
            };
            SelectionEffectivelyChanged += (_, o) => EffectivelySelectedItem = o;
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            if (oldValue != null)
            {
                var view = CollectionViewSource.GetDefaultView(oldValue);
                view.Filter = orgViewFilter;
                view.SortDescriptions.Remove(sorter);
                view.CollectionChanged -= View_CollectionChanged;
            }

            if (newValue != null)
            {
                var view = CollectionViewSource.GetDefaultView(newValue);
                orgViewFilter = view.Filter;
                view.Filter = FilterItem;
                view.SortDescriptions.Add(sorter);
                view.CollectionChanged += View_CollectionChanged;
            }

            lockFinalSelection = true;
            base.OnItemsSourceChanged(oldValue, newValue);
            lockFinalSelection = false;
        }

        private void View_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            CheckSelectedItem();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Down && !IsDropDownOpen)
            {
                IsDropDownOpen = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ClearFilter();
                Text = "";
                IsDropDownOpen = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                CheckSelectedItem();
                TriggerSelectedItemChanged();
            }
        }

        protected override void OnPreviewLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnPreviewLostKeyboardFocus(e);
            CheckSelectedItem();
            if ((e.OldFocus == this || e.OldFocus == EditableTextBox) && e.NewFocus != this && e.NewFocus != EditableTextBox)
                TriggerSelectedItemChanged();
        }

        private string GetItemDescription(object item)
        {
            if (!string.IsNullOrEmpty(DisplayMemberPath) && item != null)
            {
                var itemDescBinding = new Binding
                {
                    Source = item,
                    Path = new PropertyPath(DisplayMemberPath),
                    Mode = BindingMode.OneWay
                };
                BindingOperations.SetBinding(this, ItemDescEvaluatorProperty, itemDescBinding);

                string descValue = ItemDescEvaluator;
                return descValue;
            }

            return item?.ToString();
        }

        private void CheckSelectedItem()
        {
            Text = GetItemDescription(SelectedItem);
        }

        private bool shouldTriggerSelectedItemChanged = false;
        private void TriggerSelectedItemChanged()
        {
            if (shouldTriggerSelectedItemChanged)
            {
                SelectionEffectivelyChanged?.Invoke(this, SelectedItem);
                shouldTriggerSelectedItemChanged = false;
            }
        }

        public void ClearFilter()
        {
            if (string.IsNullOrEmpty(currentFilter)) return;
            currentFilter = "";
            CollectionViewSource.GetDefaultView(ItemsSource).Refresh();
        }

        private void FilteredComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (IsDropDownOpenUC.IsUserChange)
                ClearFilter();
        }

        private void FilteredComboBox_UserTextChange(object sender, EventArgs e)
        {
            if (isTextBoxFreezed) return;
            var tb = EditableTextBox;
            if (tb.SelectionStart + tb.SelectionLength == tb.Text.Length)
                currentFilter = tb.Text.Substring(0, tb.SelectionStart).ToLower();
            else
                currentFilter = tb.Text.ToLower();
            RefreshFilter();
        }

        private void RefreshFilter()
        {
            if (ItemsSource == null) return;

            var view = CollectionViewSource.GetDefaultView(ItemsSource);
            FreezTextBoxState(() =>
            {
                var isDropDownOpen = IsDropDownOpen;
                //always hide because showing it enables the user to pick with up and down keys, otherwise it's not working because of the glitch in view.Refresh()
                IsDropDownOpenUC.Set(false);
                view.Refresh();

                if (!string.IsNullOrEmpty(currentFilter) || isDropDownOpen)
                    IsDropDownOpenUC.Set(true);

                if (SelectedItem == null)
                {
                    foreach (var itm in ItemsSource)
                        if (GetItemDescription(itm) == Text)
                        {
                            SelectedItem = itm;
                            break;
                        }
                }
            });
        }

        private void FreezTextBoxState(Action action)
        {
            isTextBoxFreezed = true;
            var tb = EditableTextBox;
            var text = Text;
            var selStart = tb.SelectionStart;
            var selLen = tb.SelectionLength;
            action();
            Text = text;
            tb.SelectionStart = selStart;
            tb.SelectionLength = selLen;
            isTextBoxFreezed = false;
        }

        private bool FilterItem(object value)
        {
            if (orgViewFilter != null && !orgViewFilter(value)) return false;
            if (value == null) return false;
            if (currentFilter.Length == 0) return true;

            return GetItemDescription(value).ToLower().Contains(currentFilter);
        }

        private class TextBoxBaseUserChangeTracker
        {
            private bool IsTextInput { get; set; }

            public TextBoxBase TextBoxBase { get; set; }
            private List<Key> PressedKeys = new List<Key>();
            public event EventHandler UserTextChanged;
            private string LastText;

            public TextBoxBaseUserChangeTracker(TextBoxBase textBoxBase)
            {
                TextBoxBase = textBoxBase;
                LastText = TextBoxBase.ToString();

                textBoxBase.PreviewTextInput += (s, e) =>
                {
                    IsTextInput = true;
                };

                textBoxBase.TextChanged += (s, e) =>
                {
                    var isUserChange = PressedKeys.Count > 0 || IsTextInput || LastText == TextBoxBase.ToString();
                    IsTextInput = false;
                    LastText = TextBoxBase.ToString();
                    if (isUserChange)
                        UserTextChanged?.Invoke(this, e);
                };

                textBoxBase.PreviewKeyDown += (s, e) =>
                {
                    switch (e.Key)
                    {
                        case Key.Back:
                        case Key.Space:
                            if (!PressedKeys.Contains(e.Key))
                                PressedKeys.Add(e.Key);
                            break;
                    }
                    if (e.Key == Key.Back)
                    {
                        var textBox = textBoxBase as TextBox;
                        if (textBox.SelectionStart > 0 && textBox.SelectionLength > 0 && (textBox.SelectionStart + textBox.SelectionLength) == textBox.Text.Length)
                        {
                            textBox.SelectionStart--;
                            textBox.SelectionLength++;
                            e.Handled = true;
                            UserTextChanged?.Invoke(this, e);
                        }
                    }
                };

                textBoxBase.PreviewKeyUp += (s, e) =>
                {
                    if (PressedKeys.Contains(e.Key))
                        PressedKeys.Remove(e.Key);
                };

                textBoxBase.LostFocus += (s, e) =>
                {
                    PressedKeys.Clear();
                    IsTextInput = false;
                };
            }
        }

        private class UserChange<T>
        {
            private Action<T> action;

            public bool IsUserChange { get; private set; } = true;

            public UserChange(Action<T> action)
            {
                this.action = action;
            }

            public void Set(T val)
            {
                try
                {
                    IsUserChange = false;
                    action(val);
                }
                finally
                {
                    IsUserChange = true;
                }
            }
        }
    }
}
