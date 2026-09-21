using Microsoft.Win32;
using OpenCvSharp;
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
        private readonly DiagramViscosityIsolineRecognitionService _isolineRecognitionService;

        private Mat? _sourceImage;
        private string? _sourceFilePath;

        private GridRecognitionResult? _recognitionResult;
        private ViscosityIsolineRecognitionResult? _isolineRecognitionResult;

        public AddDiagramWindow()
        {
            InitializeComponent();

            _diagramService = new DiagramService();
            _recognitionService = new DiagramRecognitionService();
            _isolineRecognitionService = new DiagramViscosityIsolineRecognitionService();
        }

        private void SelectDiagramButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите диаграмму",
                Filter =
                    "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|" +
                    "Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _sourceFilePath = dialog.FileName;

                _sourceImage?.Dispose();
                _sourceImage = Cv2.ImRead(
                    dialog.FileName,
                    ImreadModes.Color);

                if (_sourceImage.Empty())
                {
                    MessageBox.Show(
                        "Не удалось открыть изображение.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    _sourceImage.Dispose();
                    _sourceImage = null;
                    _sourceFilePath = null;

                    return;
                }

                SelectedFileTextBlock.Text = dialog.FileName;

                PreviewPlaceholder.Visibility = Visibility.Collapsed;

                ShowPreview(_sourceImage);

                _recognitionResult = null;
                _isolineRecognitionResult = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка открытия изображения:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RecognizeGridButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceImage == null || _sourceImage.Empty())
            {
                MessageBox.Show(
                    "Сначала выберите изображение диаграммы.",
                    "Распознавание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                // Распознавание синей сетки
                _recognitionResult =
                    _recognitionService.FindGrid(_sourceImage);

                // Распознавание зеленых изолиний вязкости
                _isolineRecognitionResult =
                    _isolineRecognitionService.FindIsolines(_sourceImage);

                using var preview = _sourceImage.Clone();

                // Отрисовываем найденную сетку
                _recognitionService.DrawDetectedGrid(
                    preview,
                    _recognitionResult);

                // Отрисовываем найденные изолинии
                DrawRecognizedIsolines(
                    preview,
                    _isolineRecognitionResult);

                ShowPreview(preview);

                MessageBox.Show(
                    $"Распознавание завершено.\n\n" +
                    $"Линий сетки: {_recognitionResult.Lines.Count}\n" +
                    $"Изолиний вязкости: {_isolineRecognitionResult.Isolines.Count}",
                    "Результат распознавания",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка распознавания:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceImage == null || _sourceImage.Empty())
            {
                MessageBox.Show(
                    "Сначала выберите изображение диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(_sourceFilePath))
            {
                MessageBox.Show(
                    "Не найден путь к изображению диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show(
                    "Введите название диаграммы.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NameTextBox.Focus();
                return;
            }

            if (!double.TryParse(
                    Al2O3TextBox.Text.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
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

            if (!double.TryParse(
                    TemperatureTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
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

            try
            {
                // Если пользователь не запускал распознавание вручную,
                // выполняем его автоматически перед открытием редактора.
                if (_recognitionResult == null)
                {
                    _recognitionResult =
                        _recognitionService.FindGrid(_sourceImage);
                }

                if (_isolineRecognitionResult == null)
                {
                    _isolineRecognitionResult =
                        _isolineRecognitionService.FindIsolines(_sourceImage);
                }

                // Сохраняем диаграмму через существующий DiagramService.
                // Ему нужен путь к исходному файлу, а не OpenCvSharp.Mat.
                var diagram = _diagramService.AddDiagram(
                    NameTextBox.Text.Trim(),
                    al2o3,
                    temperature,
                    _sourceFilePath);

                // Открываем редактор сетки и изолиний.
                var editor = new DiagramGridEditorWindow(
                    diagram.Id,
                    _sourceImage,
                    _recognitionResult,
                    _isolineRecognitionResult);

                editor.ShowDialog();

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка добавления диаграммы:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowPreview(Mat image)
        {
            Cv2.ImEncode(
                ".png",
                image,
                out byte[] bytes);

            var bitmap = new BitmapImage();

            using var stream = new MemoryStream(bytes);

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            PreviewImage.Source = bitmap;
        }

        private void DrawRecognizedIsolines(
            Mat image,
            ViscosityIsolineRecognitionResult result)
        {
            foreach (var isoline in result.Isolines)
            {
                var points = isoline.Points;

                if (points.Count < 2)
                    continue;

                var cvPoints = points
                    .Select(p =>
                        new OpenCvSharp.Point(
                            (int)(p.X * image.Width),
                            (int)(p.Y * image.Height)))
                    .ToArray();

                if (cvPoints.Length < 2)
                    continue;

                Cv2.Polylines(
                    image,
                    new[] { cvPoints },
                    false,
                    new Scalar(0, 255, 0),
                    3);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _sourceImage?.Dispose();

            base.OnClosed(e);
        }
    }
}