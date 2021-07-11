using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    /// <summary>
    /// Interaction logic for NumTextBox.xaml
    /// </summary>
    public partial class NumTextBox : UserControl
    {
        public static readonly DependencyProperty MinValueProperty = DependencyProperty.RegisterAttached("MinValue", typeof(int), typeof(NumTextBox),
            new PropertyMetadata(1, (d, e) => (d as NumTextBox)?.OnMinValueChanged((int)e.NewValue)));

        public int MinValue
        {
            get { return (int)GetValue(MinValueProperty); }
            set { SetValue(MinValueProperty, value); }
        }

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.RegisterAttached("MaxValue", typeof(int), typeof(NumTextBox),
            new PropertyMetadata(10, (d, e) => (d as NumTextBox)?.OnMaxValueChanged((int)e.NewValue)));

        public int MaxValue
        {
            get { return (int)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(int), typeof(NumTextBox),
            new PropertyMetadata(1, (d, e) => (d as NumTextBox)?.OnValueChanged((int)e.NewValue)));

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private bool lockTextUpdate = false;

        public NumTextBox()
        {
            InitializeComponent();

            textNum.PreviewTextInput += TextNum_PreviewTextInput;
            textNum.TextChanged += TextNum_TextChanged;

            textNum.PreviewMouseLeftButtonDown += TextNum_PreviewMouseLeftButtonDown;
            textNum.GotKeyboardFocus += TextNum_GotKeyboardFocus;
            textNum.MouseDoubleClick += TextNum_MouseDoubleClick;

            UpdateMask();
            UpdateText();
        }

        private void TextNum_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            textNum.SelectAll();
        }

        private void TextNum_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            textNum.SelectAll();
        }

        private void TextNum_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = ViewUtils.FindVisualParent(e.OriginalSource as UIElement, x => x is TextBox) as TextBox;
            if (textBox == textNum && !textNum.IsKeyboardFocusWithin)
            {
                textBox.Focus();
                e.Handled = true;
            }
        }

        private void TextNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool isValid = int.TryParse(textNum.Text, out int newValue);
            if (isValid)
            {
                isValid = (newValue >= MinValue) && (newValue <= MaxValue);
            }

            if (!isValid)
            {
                textNum.Text = Value.ToString();
                textNum.SelectAll();
            }
            else
            {
                lockTextUpdate = true;
                Value = newValue;
                lockTextUpdate = false;
            }
        }

        private void TextNum_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == null || !e.Text.All(char.IsDigit))
            {
                e.Handled = true;
            }
        }

        public void OnMinValueChanged(int minValue)
        {
            if (Value < minValue)
            {
                Value = minValue;
            }
        }

        public void OnMaxValueChanged(int maxValue)
        {
            if (Value > maxValue)
            {
                Value = maxValue;
                UpdateMask();
            }
        }

        public void OnValueChanged(int newValue)
        {
            UpdateText();
        }

        private void UpdateMask()
        {
            string newMask = "##"; // 1 more for nicer presentation
            int testV = MaxValue;
            while (testV >= 10)
            {
                testV /= 10;
                newMask += '#';
            }

            textMarker.Text = newMask;
        }

        private void UpdateText()
        {
            if (!lockTextUpdate)
            {
                textNum.Text = Value.ToString();
            }
        }

        private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                RepeatButtonUp_Click(null, null);
            }
            else if (e.Delta < 0)
            {
                RepeatButtonDown_Click(null, null);
            }
        }

        private void RepeatButtonUp_Click(object sender, RoutedEventArgs e)
        {
            if (Value < MaxValue)
            {
                Value++;
            }
        }

        private void RepeatButtonDown_Click(object sender, RoutedEventArgs e)
        {
            if (Value > MinValue)
            {
                Value--;
            }
        }
    }
}
