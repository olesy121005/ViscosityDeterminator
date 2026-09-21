using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using ViscosityDeterminator.Models;
using ViscosityDeterminator.Services;

namespace ViscosityDeterminator
{
    public partial class AddDiagramWindow : System.Windows.Window
    {
        private readonly DiagramService _diagramService;
        private readonly DiagramRecognitionService _recognitionService;

        private string? _selectedFilePath;

        private Mat? _sourceImage;

        private GridRecognitionResult?
            _recognitionResult;

        public AddDiagramWindow()
        {
            InitializeComponent();

            _diagramService =
                new DiagramService();

            _diagramService.InitializeDatabase();

            _recognitionService =
                new DiagramRecognitionService();
        }

        private void SelectDiagramButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new OpenFileDialog
                {
                    Filter =
                        "Изображения|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*"
                };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _sourceImage?.Dispose();

                _selectedFilePath =
                    dialog.FileName;

                _sourceImage =
                    _recognitionService.LoadImage(
                        _selectedFilePath);

                PreviewImage.Source =
                    ConvertMatToBitmapImage(
                        _sourceImage);

                PreviewImage.Visibility =
                    Visibility.Visible;

                PreviewPlaceholder.Visibility =
                    Visibility.Collapsed;

                SelectedFileTextBlock.Text =
                    _selectedFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки изображения:\n\n{ex.Message}",
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
                    "Сначала выберите изображение диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                _recognitionResult =
                    _recognitionService.FindGrid(
                        _sourceImage);

                using var result =
                    _recognitionService.DrawDetectedGrid(
                        _sourceImage,
                        _recognitionResult);

                PreviewImage.Source =
                    ConvertMatToBitmapImage(
                        result);

                MessageBox.Show(
                    $"Распознано линий: " +
                    $"{_recognitionResult.Lines.Count}\n\n" +
                    "Теперь добавьте диаграмму, после чего " +
                    "откроется редактор сетки.",
                    "Распознавание завершено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка распознавания сетки:\n\n{ex.Message}",
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

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    _selectedFilePath))
            {
                MessageBox.Show(
                    "Сначала выберите файл диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!TryReadValue(
                    Al2O3TextBox.Text,
                    out double al2o3))
            {
                MessageBox.Show(
                    "Введите корректное значение Al₂O₃.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!TryReadValue(
                    TemperatureTextBox.Text,
                    out double temperature))
            {
                MessageBox.Show(
                    "Введите корректную температуру.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (al2o3 != 5 &&
                al2o3 != 10 &&
                al2o3 != 15)
            {
                MessageBox.Show(
                    "Al₂O₃ может быть только 5, 10 или 15 %.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (temperature != 1400 &&
                temperature != 1450 &&
                temperature != 1500)
            {
                MessageBox.Show(
                    "Температура может быть только 1400, 1450 или 1500 °C.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                // Если сетку ещё не распознавали,
                // распознаём автоматически.
                if (_sourceImage == null ||
                    _sourceImage.Empty())
                {
                    throw new InvalidOperationException(
                        "Изображение диаграммы не загружено.");
                }

                if (_recognitionResult == null)
                {
                    _recognitionResult =
                        _recognitionService.FindGrid(
                            _sourceImage);
                }

                // Сохраняем диаграмму.
                _diagramService.AddDiagram(
                    name,
                    al2o3,
                    temperature,
                    _selectedFilePath);

                // Получаем добавленную диаграмму.
                Diagram? diagram =
                    _diagramService.FindDiagram(
                        al2o3,
                        temperature);

                if (diagram == null)
                {
                    throw new InvalidOperationException(
                        "Не удалось получить добавленную диаграмму.");
                }

                // Открываем редактор сетки.
                var editor =
                    new DiagramGridEditorWindow(
                        diagram.Id,
                        _sourceImage,
                        _recognitionResult)
                    {
                        Owner = this
                    };

                editor.ShowDialog();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при добавлении диаграммы:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool TryReadValue(
            string text,
            out double value)
        {
            text =
                text.Trim()
                    .Replace(',', '.');

            return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static BitmapImage
            ConvertMatToBitmapImage(Mat image)
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