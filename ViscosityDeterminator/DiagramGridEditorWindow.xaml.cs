using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OpenCvSharp;
using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;
using ViscosityDeterminator.Services;

using WpfPoint = System.Windows.Point;

namespace ViscosityDeterminator
{
    public partial class DiagramGridEditorWindow : System.Windows.Window
    {
        private readonly int _diagramId;

        private readonly DiagramGridService _gridService;

        private Mat _image;

        private readonly ObservableCollection<
            DiagramGridLineViewModel> _lines = new();

        private DiagramGridLineViewModel? _selectedLine;

        private WpfPoint? _manualStartPoint;

        public DiagramGridEditorWindow(
            int diagramId,
            Mat image,
            GridRecognitionResult recognitionResult)
        {
            InitializeComponent();

            _diagramId = diagramId;

            _gridService =
                new DiagramGridService(
                    new KnowledgeBaseContext());

            _image = image.Clone();

            LinesListBox.ItemsSource =
                _lines;

            LoadRecognizedLines(
                recognitionResult);

            LoadImage();

            Loaded +=
                DiagramGridEditorWindow_Loaded;
        }

        private void DiagramGridEditorWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            DrawLines();
        }

        private void LoadImage()
        {
            DiagramImage.Source =
                ConvertMatToBitmapImage(
                    _image);
        }

        private void LoadRecognizedLines(
            GridRecognitionResult result)
        {
            _lines.Clear();

            foreach (var line in result.Lines)
            {
                var model =
                    new DiagramGridLine
                    {
                        DiagramId =
                            _diagramId,

                        Component =
                            line.Component,

                        Value = 0,

                        X1 =
                            line.P1.X /
                            _image.Width,

                        Y1 =
                            line.P1.Y /
                            _image.Height,

                        X2 =
                            line.P2.X /
                            _image.Width,

                        Y2 =
                            line.P2.Y /
                            _image.Height,

                        IsVerified = false,

                        Confidence =
                            line.Confidence
                    };

                _lines.Add(
                    new DiagramGridLineViewModel(
                        model));
            }
        }

        private void LinesListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (LinesListBox.SelectedItem
                is not DiagramGridLineViewModel selected)
            {
                _selectedLine = null;
                return;
            }

            _selectedLine = selected;

            ComponentComboBox.SelectedIndex =
                selected.Component switch
                {
                    "CaO" => 0,
                    "MgO" => 1,
                    "SiO2" => 2,
                    _ => 0
                };

            ValueTextBox.Text =
                selected.Value == 0
                    ? string.Empty
                    : selected.Value
                        .ToString(
                            CultureInfo.InvariantCulture);

            DrawLines();
        }

        private void UpdateLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedLine == null)
            {
                MessageBox.Show(
                    "Сначала выберите линию.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string component =
                GetSelectedComponent();

            if (!TryReadValue(
                    ValueTextBox.Text,
                    out double value))
            {
                MessageBox.Show(
                    "Введите корректное значение.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            _selectedLine.Component =
                component;

            _selectedLine.Value =
                value;

            _selectedLine.IsVerified =
                true;

            LinesListBox.Items.Refresh();

            DrawLines();
        }

        private void AddManualLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Для добавления линии вручную нажмите " +
                "на её начальную точку, а затем на конечную точку " +
                "на диаграмме.",
                "Добавление линии",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _manualStartPoint = null;
        }

        private void DiagramCanvas_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            WpfPoint point =
                e.GetPosition(
                    DiagramCanvas);

            if (_manualStartPoint == null)
            {
                _manualStartPoint =
                    point;

                DrawTemporaryPoint(
                    point);

                return;
            }

            WpfPoint start =
                _manualStartPoint.Value;

            WpfPoint end =
                point;

            _manualStartPoint = null;

            string component =
                GetSelectedComponent();

            if (!TryReadValue(
                    ValueTextBox.Text,
                    out double value))
            {
                MessageBox.Show(
                    "Перед добавлением линии " +
                    "укажите значение линии.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var line =
                new DiagramGridLine
                {
                    DiagramId =
                        _diagramId,

                    Component =
                        component,

                    Value =
                        value,

                    X1 =
                        start.X /
                        DiagramCanvas.ActualWidth,

                    Y1 =
                        start.Y /
                        DiagramCanvas.ActualHeight,

                    X2 =
                        end.X /
                        DiagramCanvas.ActualWidth,

                    Y2 =
                        end.Y /
                        DiagramCanvas.ActualHeight,

                    IsVerified = true,

                    Confidence = 1.0
                };

            var viewModel =
                new DiagramGridLineViewModel(
                    line);

            _lines.Add(
                viewModel);

            LinesListBox.SelectedItem =
                viewModel;

            DrawLines();
        }

        private void DeleteLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedLine == null)
            {
                MessageBox.Show(
                    "Выберите линию для удаления.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            _lines.Remove(
                _selectedLine);

            _selectedLine = null;

            DrawLines();
        }

        private void SaveGridButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_lines.Count == 0)
            {
                MessageBox.Show(
                    "Сетка не содержит линий.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var unverified =
                _lines
                    .Where(x =>
                        !x.IsVerified)
                    .ToList();

            if (unverified.Count > 0)
            {
                MessageBox.Show(
                    "Не все линии проверены.\n\n" +
                    "Для каждой линии необходимо " +
                    "указать компонент и значение.",
                    "Сетка не готова",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                var models =
                    _lines
                        .Select(x => x.Model)
                        .ToList();

                _gridService.ReplaceLines(
                    _diagramId,
                    models);

                MessageBox.Show(
                    "Сетка диаграммы успешно сохранена.",
                    "Готово",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка сохранения сетки:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DrawLines()
        {
            if (DiagramCanvas.ActualWidth <= 0 ||
                DiagramCanvas.ActualHeight <= 0)
            {
                return;
            }

            DiagramCanvas.Children.Clear();

            foreach (var line in _lines)
            {
                double x1 =
                    line.X1 *
                    DiagramCanvas.ActualWidth;

                double y1 =
                    line.Y1 *
                    DiagramCanvas.ActualHeight;

                double x2 =
                    line.X2 *
                    DiagramCanvas.ActualWidth;

                double y2 =
                    line.Y2 *
                    DiagramCanvas.ActualHeight;

                var shape =
                    new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,

                        Stroke =
                            GetComponentColor(
                                line.Component),

                        StrokeThickness =
                            ReferenceEquals(
                                line,
                                _selectedLine)
                                ? 4
                                : 2,

                        Opacity =
                            line.IsVerified
                                ? 1.0
                                : 0.7
                    };

                DiagramCanvas.Children.Add(
                    shape);
            }
        }

        private void DrawTemporaryPoint(
            WpfPoint point)
        {
            var ellipse =
                new Ellipse
                {
                    Width = 10,
                    Height = 10,

                    Fill =
                        Brushes.White,

                    Stroke =
                        Brushes.Red,

                    StrokeThickness = 2
                };

            Canvas.SetLeft(
                ellipse,
                point.X - 5);

            Canvas.SetTop(
                ellipse,
                point.Y - 5);

            DiagramCanvas.Children.Add(
                ellipse);
        }

        private string GetSelectedComponent()
        {
            if (ComponentComboBox.SelectedItem
                is ComboBoxItem item)
            {
                return item.Content?
                    .ToString() ?? "CaO";
            }

            return "CaO";
        }

        private static Brush GetComponentColor(
            string component)
        {
            return component switch
            {
                "CaO" =>
                    Brushes.Red,

                "MgO" =>
                    Brushes.LimeGreen,

                "SiO2" =>
                    Brushes.Gold,

                _ =>
                    Brushes.White
            };
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
            _image.Dispose();

            base.OnClosed(e);
        }
    }

    public class DiagramGridLineViewModel
    {
        public DiagramGridLine Model { get; }

        public string Component
        {
            get => Model.Component;
            set => Model.Component = value;
        }

        public double Value
        {
            get => Model.Value;
            set => Model.Value = value;
        }

        public double X1
        {
            get => Model.X1;
            set => Model.X1 = value;
        }

        public double Y1
        {
            get => Model.Y1;
            set => Model.Y1 = value;
        }

        public double X2
        {
            get => Model.X2;
            set => Model.X2 = value;
        }

        public double Y2
        {
            get => Model.Y2;
            set => Model.Y2 = value;
        }

        public bool IsVerified
        {
            get => Model.IsVerified;
            set => Model.IsVerified = value;
        }

        public double Confidence
        {
            get => Model.Confidence;
            set => Model.Confidence = value;
        }

        public string DisplayText
        {
            get
            {
                string valueText =
                    Value == 0
                        ? "значение не задано"
                        : Value.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture);

                return
                    $"{Component} = {valueText} " +
                    $"(уверенность {Confidence:P0})";
            }
        }

        public DiagramGridLineViewModel(
            DiagramGridLine model)
        {
            Model = model;
        }
    }
}