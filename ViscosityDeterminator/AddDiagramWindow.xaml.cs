using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ViscosityDeterminator.Services;

namespace ViscosityDeterminator
{
    public partial class AddDiagramWindow : System.Windows.Window
    {
        private readonly DiagramService _diagramService;
        private readonly DiagramRecognitionService _recognitionService;
        private string? _selectedFilePath;
        private Mat? _sourceImage;

        public AddDiagramWindow()
        {
            InitializeComponent();

            _diagramService = new DiagramService();
            _diagramService.InitializeDatabase();

            _recognitionService = new DiagramRecognitionService();
        }

        private void SelectDiagramButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите диаграмму",
                Filter =
                    "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|" +
                    "Все файлы|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _selectedFilePath =
                    dialog.FileName;

                SelectedFileTextBlock.Text =
                    _selectedFilePath;

                _sourceImage =
                    _recognitionService.LoadImage(
                        _selectedFilePath);

                PreviewImage.Source =
                    ConvertMatToBitmapImage(
                        _sourceImage);

                PreviewPlaceholder.Visibility =
                    Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _selectedFilePath = null;

                _sourceImage?.Dispose();
                _sourceImage = null;

                PreviewImage.Source = null;

                PreviewPlaceholder.Visibility =
                    Visibility.Visible;

                SelectedFileTextBlock.Text =
                    string.Empty;

                MessageBox.Show(
                    $"Не удалось загрузить диаграмму:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RecognizeGridButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_sourceImage == null ||
                _sourceImage.Empty())
            {
                MessageBox.Show(
                    "Сначала выберите диаграмму.",
                    "Распознавание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                var grid =
                    _recognitionService.FindGrid(
                        _sourceImage);

                if (grid.Lines.Count == 0)
                {
                    MessageBox.Show(
                        "Не удалось распознать координатную сетку.",
                        "Распознавание",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                using var result =
                    _recognitionService.DrawDetectedGrid(
                        _sourceImage,
                        grid);

                PreviewImage.Source =
                    ConvertMatToBitmapImage(
                        result);

                MessageBox.Show(
                    $"Распознано линий сетки: {grid.Lines.Count}",
                    "Распознавание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка распознавания:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string name =
                NameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "Введите название диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NameTextBox.Focus();

                return;
            }

            if (!TryParseNumber(
                    Al2O3TextBox.Text,
                    out double al2o3))
            {
                MessageBox.Show(
                    "Введите корректное значение Al₂O₃.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Al2O3TextBox.Focus();

                return;
            }

            if (al2o3 != 5 &&
                al2o3 != 10 &&
                al2o3 != 15)
            {
                MessageBox.Show(
                    "Содержание Al₂O₃ должно быть 5, 10 или 15 %.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Al2O3TextBox.Focus();

                return;
            }

            if (!TryParseNumber(
                    TemperatureTextBox.Text,
                    out double temperature))
            {
                MessageBox.Show(
                    "Введите корректную температуру.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TemperatureTextBox.Focus();

                return;
            }

            if (temperature != 1400 &&
                temperature != 1450 &&
                temperature != 1500)
            {
                MessageBox.Show(
                    "Температура должна быть 1400, 1450 или 1500 °C.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TemperatureTextBox.Focus();

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    _selectedFilePath) ||
                !File.Exists(
                    _selectedFilePath))
            {
                MessageBox.Show(
                    "Выберите файл диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                _diagramService.AddDiagram(
                    name,
                    al2o3,
                    temperature,
                    _selectedFilePath);

                MessageBox.Show(
                    "Диаграмма успешно добавлена в базу знаний.",
                    "Добавление диаграммы",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось добавить диаграмму:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private bool TryParseNumber(
            string text,
            out double value)
        {
            text = text.Trim();

            if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value))
            {
                return true;
            }

            if (double.TryParse(
                    text.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        private BitmapImage ConvertMatToBitmapImage(
            Mat image)
        {
            Cv2.ImEncode(
                ".png",
                image,
                out var buffer);

            using var stream =
                new MemoryStream(buffer);

            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;
            bitmap.StreamSource =
                stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        protected override void OnClosed(
            EventArgs e)
        {
            _sourceImage?.Dispose();
            base.OnClosed(e);
        }
    }
}