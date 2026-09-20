using OpenCvSharp;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ViscosityDeterminator.Models;
using ViscosityDeterminator.Services;

namespace ViscosityDeterminator
{
    public partial class KnowledgeBaseWindow : System.Windows.Window
    {
        private readonly DiagramService _diagramService;
        private readonly DiagramRecognitionService _recognitionService;

        public KnowledgeBaseWindow()
        {
            InitializeComponent();

            _diagramService = new DiagramService();
            _diagramService.InitializeDatabase();

            _recognitionService = new DiagramRecognitionService();

            LoadDiagrams();
        }

        private void LoadDiagrams()
        {
            try
            {
                var diagrams = _diagramService.GetDiagrams();
                DiagramsListBox.ItemsSource = diagrams;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось загрузить базу знаний:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DiagramsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DiagramsListBox.SelectedItem is not Diagram diagram)
                return;

            DisplayDiagram(diagram);
        }

        private void DisplayDiagram(Diagram diagram)
        {
            try
            {
                DiagramNameTextBlock.Text = diagram.Name;
                DiagramTemperatureTextBlock.Text = $"{diagram.Temperature:F0} °C";

                using var sourceImage =
                    Cv2.ImDecode(
                        diagram.ImageData,
                        ImreadModes.Color);

                if (sourceImage.Empty())
                {
                    BoundaryStatusTextBlock.Text =
                        "Ошибка загрузки изображения";

                    PreviewImage.Source = null;
                    PreviewPlaceholder.Visibility =
                        Visibility.Visible;

                    return;
                }

                var grid =
                    _recognitionService.FindGrid(
                        sourceImage);

                if (grid.Lines.Count == 0)
                {
                    BoundaryStatusTextBlock.Text =
                        "Сетка не распознана";

                    PreviewImage.Source =
                        ConvertMatToBitmapImage(
                            sourceImage);

                    PreviewPlaceholder.Visibility =
                        Visibility.Collapsed;

                    return;
                }

                using var result =
                    _recognitionService.DrawDetectedGrid(
                        sourceImage,
                        grid);

                PreviewImage.Source =
                    ConvertMatToBitmapImage(result);

                PreviewPlaceholder.Visibility =
                    Visibility.Collapsed;

                BoundaryStatusTextBlock.Text =
                    $"Распознано линий сетки: {grid.Lines.Count}";
            }
            catch (Exception ex)
            {
                BoundaryStatusTextBlock.Text =
                    "Ошибка";

                MessageBox.Show(
                    $"Ошибка при отображении диаграммы:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private BitmapImage ConvertMatToBitmapImage(Mat image)
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

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}