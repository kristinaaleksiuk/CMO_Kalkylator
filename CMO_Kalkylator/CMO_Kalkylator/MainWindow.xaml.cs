using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CMO_Kalkylator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");
        private readonly List<MenuItem> _menuItems = new List<MenuItem>();

        public MainWindow()
        {
            InitializeComponent();

            // Заполняем и выпадающий список, и главное меню одним и тем же набором типов СМО
            foreach (var info in QueueingCalculator.Types)
            {
                TypeCombo.Items.Add(info);

                var item = new MenuItem { Header = info.Title, Tag = info, IsCheckable = true };
                item.Click += TypeMenuItem_Click;
                TypeMenuRoot.Items.Add(item);
                _menuItems.Add(item);
            }
            TypeCombo.SelectedIndex = 3;   // по умолчанию — многоканальная с неограниченной очередью
            UpdateConvertedText();
        }

        private SmoTypeInfo CurrentType => (SmoTypeInfo)TypeCombo.SelectedItem;

        // ---------------------------- Выбор типа СМО ----------------------------------------
        private void TypeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var info = (SmoTypeInfo)((MenuItem)sender).Tag;
            TypeCombo.SelectedItem = info;
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var info = CurrentType;
            if (info == null) return;

            foreach (var mi in _menuItems) mi.IsChecked = ReferenceEquals(mi.Tag, info);

            DescriptionText.Text = info.Description;
            ChannelsPanel.Visibility = info.ShowChannels ? Visibility.Visible : Visibility.Collapsed;
            QueuePanel.Visibility = info.ShowQueueLimit ? Visibility.Visible : Visibility.Collapsed;
            PatiencePanel.Visibility = info.ShowPatience ? Visibility.Visible : Visibility.Collapsed;

            ResultsGrid.ItemsSource = null;
            StatesGrid.ItemsSource = null;
            WarningBorder.Visibility = Visibility.Collapsed;
        }

        private void Mode_Changed(object sender, SelectionChangedEventArgs e) => UpdateConvertedText();

        private void UpdateConvertedText()
        {
            if (ConvertedText == null) return;
            ConvertedText.Text = "";
        }

        // ------------------------------- Ввод данных ----------------------------------------
        private static bool TryParseDouble(string text, out double value)
        {
            text = (text ?? "").Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private bool ReadPositive(TextBox box, string name, out double value)
        {
            if (!TryParseDouble(box.Text, out value) || value <= 0)
            {
                MessageBox.Show($"Поле «{name}» должно быть положительным числом.", "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                box.Focus();
                box.SelectAll();
                return false;
            }
            return true;
        }

        private bool ReadInt(TextBox box, string name, int min, out int value)
        {
            if (!int.TryParse(box.Text.Trim(), out value) || value < min)
            {
                MessageBox.Show($"Поле «{name}» должно быть целым числом не меньше {min}.", "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                box.Focus();
                box.SelectAll();
                return false;
            }
            return true;
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            var info = CurrentType;
            if (info == null) return;

            // --- поток заявок: либо интенсивность λ, либо среднее время между заявками ---
            if (!ReadPositive(LambdaBox, LambdaMode.SelectedIndex == 0 ? "λ" : "Среднее время между заявками", out double lambdaRaw))
                return;
            double lambda = LambdaMode.SelectedIndex == 0 ? lambdaRaw : 1.0 / lambdaRaw;

            // --- поток обслуживания: либо интенсивность μ, либо среднее время обслуживания ---
            if (!ReadPositive(MuBox, MuMode.SelectedIndex == 0 ? "μ" : "Среднее время обслуживания", out double muRaw))
                return;
            double mu = MuMode.SelectedIndex == 0 ? muRaw : 1.0 / muRaw;

            int n = 1, m = 0;
            double nu = 0;

            if (info.ShowChannels && !ReadInt(ChannelsBox, "Число каналов n", 1, out n)) return;
            if (info.ShowQueueLimit && !ReadInt(QueueBox, "Максимум мест в очереди m", 1, out m)) return;

            if (info.ShowPatience)
            {
                if (!ReadPositive(NuBox, NuMode.SelectedIndex == 0 ? "Среднее время ожидания" : "ν", out double nuRaw))
                    return;
                nu = NuMode.SelectedIndex == 0 ? 1.0 / nuRaw : nuRaw;
            }

            string unit = UnitBox.Text;
            var result = QueueingCalculator.Calculate(info.Type, lambda, mu, n, m, nu, unit);

            // справка о пересчитанных параметрах
            string u = string.IsNullOrWhiteSpace(unit) ? "ед. времени" : unit.Trim();
            ConvertedText.Text =
                $"Используемые параметры:\nλ = {lambda.ToString("0.#####", Ru)} 1/{u},  " +
                $"μ = {mu.ToString("0.#####", Ru)} 1/{u}" +
                (info.ShowPatience ? $",  ν = {nu.ToString("0.#####", Ru)} 1/{u}" : "");

            ResultsGrid.ItemsSource = result.Rows;
            StatesGrid.ItemsSource = result.States;

            if (!string.IsNullOrEmpty(result.Warning))
            {
                WarningText.Text = result.Warning;
                WarningBorder.Visibility = Visibility.Visible;
            }
            else
            {
                WarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Программа анализа систем массового обслуживания.\n\n" +
                "Реализованы: СМО с отказами, с неограниченной и ограниченной очередью, " +
                "с фиксированным временем обслуживания, с ограниченным временем ожидания.\n\n" +
                "Поток заявок и поток обслуживания можно задавать интенсивностью или средним временем.",
                "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
