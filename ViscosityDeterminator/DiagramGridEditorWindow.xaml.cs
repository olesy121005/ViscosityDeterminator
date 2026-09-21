using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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

        private readonly DiagramService _diagramService;
        private readonly DiagramGridService _gridService;
        private readonly DiagramViscosityIsolineService _isolineService;

        private readonly Mat _image;

        private readonly ObservableCollection<DiagramGridLineViewModel> _gridLines =
            new();

        private readonly ObservableCollection<DiagramViscosityIsolineViewModel> _isolines =
            new();


        // =========================================================
        // РУЧНОЕ ДОБАВЛЕНИЕ ЛИНИИ
        // =========================================================

        private bool _drawingManualLine;

        private WpfPoint? _manualLineStart;


        // =========================================================
        // РУЧНОЕ ДОБАВЛЕНИЕ ИЗОЛИНИИ
        // =========================================================

        private bool _drawingManualIsoline;

        private Polyline? _manualIsolinePolyline;

        private readonly List<WpfPoint> _manualIsolinePoints =
            new();


        // =========================================================
        // ПЕРЕТАСКИВАНИЕ КОНЦА СОХРАНЁННОЙ ЛИНИИ
        // =========================================================

        private DiagramGridLineViewModel? _draggingLine;

        private bool _draggingStartPoint;

        private bool _draggingEndPoint;

        private bool _isDragging;


        // =========================================================
        // КОНСТРУКТОР НОВОЙ ДИАГРАММЫ
        // =========================================================

        public DiagramGridEditorWindow(
            int diagramId,
            Mat image,
            GridRecognitionResult recognitionResult,
            ViscosityIsolineRecognitionResult? isolineRecognitionResult = null)
        {
            InitializeComponent();

            _diagramId = diagramId;

            _image = image;

            _diagramService =
                new DiagramService();

            var context =
                new KnowledgeBaseContext();

            _gridService =
                new DiagramGridService(context);

            _isolineService =
                new DiagramViscosityIsolineService(context);

            LoadDiagramInfo();

            LoadGrid(
                recognitionResult);

            if (isolineRecognitionResult != null)
            {
                LoadRecognizedIsolines(
                    isolineRecognitionResult);
            }

            InitializeLists();

            Loaded +=
                EditorWindow_Loaded;
        }


        // =========================================================
        // КОНСТРУКТОР РЕДАКТИРОВАНИЯ ИЗ БД
        // =========================================================

        public DiagramGridEditorWindow(
            int diagramId,
            Mat image)
        {
            InitializeComponent();

            _diagramId = diagramId;

            _image = image;

            _diagramService =
                new DiagramService();

            var context =
                new KnowledgeBaseContext();

            _gridService =
                new DiagramGridService(context);

            _isolineService =
                new DiagramViscosityIsolineService(context);

            LoadDiagramInfo();

            LoadSavedData();

            InitializeLists();

            Loaded +=
                EditorWindow_Loaded;
        }


        // =========================================================
        // СПИСКИ
        // =========================================================

        private void InitializeLists()
        {
            LinesListBox.ItemsSource =
                _gridLines;

            IsolinesListBox.ItemsSource =
                _isolines;
        }


        // =========================================================
        // ИНФОРМАЦИЯ О ДИАГРАММЕ
        // =========================================================

        private void LoadDiagramInfo()
        {
            var diagram =
                _diagramService.GetDiagram(
                    _diagramId);

            if (diagram == null)
                return;

            DiagramNameTextBox.Text =
                diagram.Name;

            Al2O3TextBox.Text =
                diagram.Al2O3.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);

            TemperatureTextBox.Text =
                diagram.Temperature.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);

            ShowImage();
        }


        // =========================================================
        // ИЗОБРАЖЕНИЕ
        // =========================================================

        private void ShowImage()
        {
            if (_image.Empty())
                return;


            // Внутренняя система координат всегда соответствует
            // исходному изображению.

            DiagramOverlay.Width =
                _image.Width;

            DiagramOverlay.Height =
                _image.Height;

            DiagramImage.Width =
                _image.Width;

            DiagramImage.Height =
                _image.Height;

            DiagramCanvas.Width =
                _image.Width;

            DiagramCanvas.Height =
                _image.Height;


            Cv2.ImEncode(
                ".png",
                _image,
                out byte[] bytes);


            using var stream =
                new MemoryStream(bytes);


            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();

            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;

            bitmap.StreamSource =
                stream;

            bitmap.EndInit();

            bitmap.Freeze();


            DiagramImage.Source =
                bitmap;
        }


        private void EditorWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            DrawAll();
        }


        // =========================================================
        // ЗАГРУЗКА РАСПОЗНАННОЙ СЕТКИ
        // =========================================================

        private void LoadGrid(
            GridRecognitionResult recognitionResult)
        {
            _gridLines.Clear();

            foreach (var line in recognitionResult.Lines)
            {
                var model =
                    new DiagramGridLine
                    {
                        DiagramId =
                            _diagramId,

                        Component =
                            line.Component,

                        Value =
                            0,

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

                        IsVerified =
                            false,

                        Confidence =
                            line.Confidence
                    };


                _gridLines.Add(
                    new DiagramGridLineViewModel(
                        model));
            }
        }


        // =========================================================
        // ЗАГРУЗКА СОХРАНЁННЫХ ДАННЫХ
        // =========================================================

        private void LoadSavedData()
        {
            var savedGridLines =
                _gridService.GetLines(
                    _diagramId);

            var savedIsolines =
                _isolineService.GetIsolines(
                    _diagramId);

            LoadSavedGrid(
                savedGridLines);

            LoadSavedIsolines(
                savedIsolines);
        }


        private void LoadSavedGrid(
            IEnumerable<DiagramGridLine> savedGridLines)
        {
            _gridLines.Clear();

            foreach (var source in savedGridLines)
            {
                var model =
                    new DiagramGridLine
                    {
                        Id =
                            source.Id,

                        DiagramId =
                            source.DiagramId,

                        Component =
                            source.Component,

                        Value =
                            source.Value,

                        X1 =
                            source.X1,

                        Y1 =
                            source.Y1,

                        X2 =
                            source.X2,

                        Y2 =
                            source.Y2,

                        IsVerified =
                            source.IsVerified,

                        Confidence =
                            source.Confidence
                    };


                _gridLines.Add(
                    new DiagramGridLineViewModel(
                        model));
            }
        }


        // =========================================================
        // ЗАГРУЗКА РАСПОЗНАННЫХ ИЗОЛИНИЙ
        // =========================================================

        private void LoadRecognizedIsolines(
            ViscosityIsolineRecognitionResult recognitionResult)
        {
            _isolines.Clear();

            foreach (var source in recognitionResult.Isolines)
            {
                var model =
                    new DiagramViscosityIsoline
                    {
                        DiagramId =
                            _diagramId,

                        Viscosity =
                            source.Viscosity,

                        Geometry =
                            source.Geometry,

                        IsVerified =
                            source.IsVerified,

                        Confidence =
                            source.Confidence
                    };


                _isolines.Add(
                    new DiagramViscosityIsolineViewModel(
                        model));
            }
        }


        // =========================================================
        // ЗАГРУЗКА СОХРАНЁННЫХ ИЗОЛИНИЙ
        // =========================================================

        private void LoadSavedIsolines(
            IEnumerable<DiagramViscosityIsoline> savedIsolines)
        {
            _isolines.Clear();

            foreach (var source in savedIsolines)
            {
                var model =
                    new DiagramViscosityIsoline
                    {
                        Id =
                            source.Id,

                        DiagramId =
                            source.DiagramId,

                        Viscosity =
                            source.Viscosity,

                        Geometry =
                            source.Geometry,

                        IsVerified =
                            source.IsVerified,

                        Confidence =
                            source.Confidence
                    };


                _isolines.Add(
                    new DiagramViscosityIsolineViewModel(
                        model));
            }
        }


        // =========================================================
        // КЛИК ПО ДИАГРАММЕ
        // =========================================================

        private void DiagramCanvas_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            var point =
                e.GetPosition(
                    DiagramCanvas);


            // =====================================================
            // РУЧНОЕ ДОБАВЛЕНИЕ НОВОЙ ЛИНИИ
            //
            // Первый клик = начало.
            // Второй клик = конец.
            // =====================================================

            if (_drawingManualLine)
            {
                if (_manualLineStart == null)
                {
                    _manualLineStart =
                        point;

                    DrawAll();

                    DrawManualLinePreview(
                        point,
                        point);
                }
                else
                {
                    var start =
                        _manualLineStart.Value;

                    var end =
                        point;

                    _drawingManualLine =
                        false;

                    _manualLineStart =
                        null;

                    CompleteManualLine(
                        start,
                        end);
                }

                e.Handled =
                    true;

                return;
            }


            // =====================================================
            // РУЧНАЯ ИЗОЛИНИЯ
            // =====================================================

            if (_drawingManualIsoline)
            {
                AddManualIsolinePoint(
                    point);

                e.Handled =
                    true;

                return;
            }


            // =====================================================
            // ПРОВЕРЯЕМ, НЕ НАЖАТ ЛИ КОНЕЦ ЛИНИИ
            // ДЛЯ ПЕРЕТАСКИВАНИЯ
            // =====================================================

            if (TryStartDraggingLinePoint(
                    point))
            {
                e.Handled =
                    true;

                return;
            }


            // =====================================================
            // ОБЫЧНЫЙ КЛИК ПО ЛИНИИ
            // =====================================================

            SelectElementAtPoint(
                point);
        }


        // =========================================================
        // НАЧАЛО ПЕРЕТАСКИВАНИЯ ТОЧКИ ЛИНИИ
        // =========================================================

        private bool TryStartDraggingLinePoint(
            WpfPoint point)
        {
            const double tolerance = 15;


            /*
             * Сначала проверяем уже выбранную линию.
             * Это делает редактирование предсказуемым:
             * выбрали линию → тянем её конец.
             */

            if (LinesListBox.SelectedItem
                is DiagramGridLineViewModel selected)
            {
                if (IsNearPoint(
                        point,
                        GetPixelPoint(
                            selected.Model.X1,
                            selected.Model.Y1),
                        tolerance))
                {
                    StartDragging(
                        selected,
                        true);

                    return true;
                }


                if (IsNearPoint(
                        point,
                        GetPixelPoint(
                            selected.Model.X2,
                            selected.Model.Y2),
                        tolerance))
                {
                    StartDragging(
                        selected,
                        false);

                    return true;
                }
            }


            /*
             * Если выбранной линии нет, проверяем все линии.
             */

            foreach (var item in _gridLines)
            {
                var model =
                    item.Model;


                if (IsNearPoint(
                        point,
                        GetPixelPoint(
                            model.X1,
                            model.Y1),
                        tolerance))
                {
                    LinesListBox.SelectedItem =
                        item;

                    StartDragging(
                        item,
                        true);

                    return true;
                }


                if (IsNearPoint(
                        point,
                        GetPixelPoint(
                            model.X2,
                            model.Y2),
                        tolerance))
                {
                    LinesListBox.SelectedItem =
                        item;

                    StartDragging(
                        item,
                        false);

                    return true;
                }
            }


            return false;
        }


        private void StartDragging(
            DiagramGridLineViewModel line,
            bool startPoint)
        {
            _draggingLine =
                line;

            _draggingStartPoint =
                startPoint;

            _draggingEndPoint =
                !startPoint;

            _isDragging =
                true;


            DiagramCanvas.CaptureMouse();
        }


        // =========================================================
        // ПЕРЕМЕЩЕНИЕ ТОЧКИ
        // =========================================================

        private void DiagramCanvas_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (!_isDragging ||
                _draggingLine == null)
            {
                return;
            }


            if (e.LeftButton != MouseButtonState.Pressed)
            {
                StopDragging();

                return;
            }


            var point =
                e.GetPosition(
                    DiagramCanvas);


            /*
             * Ограничиваем координаты областью изображения.
             */

            double x =
                Math.Max(
                    0,
                    Math.Min(
                        _image.Width,
                        point.X));


            double y =
                Math.Max(
                    0,
                    Math.Min(
                        _image.Height,
                        point.Y));


            /*
             * Переводим обратно в нормализованные
             * координаты 0..1.
             */

            double normalizedX =
                x /
                _image.Width;

            double normalizedY =
                y /
                _image.Height;


            if (_draggingStartPoint)
            {
                _draggingLine.Model.X1 =
                    normalizedX;

                _draggingLine.Model.Y1 =
                    normalizedY;
            }


            if (_draggingEndPoint)
            {
                _draggingLine.Model.X2 =
                    normalizedX;

                _draggingLine.Model.Y2 =
                    normalizedY;
            }


            DrawAll();
        }


        // =========================================================
        // ОКОНЧАНИЕ ПЕРЕТАСКИВАНИЯ
        // =========================================================

        private void DiagramCanvas_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_isDragging)
                return;


            StopDragging();

            e.Handled =
                true;
        }


        private void StopDragging()
        {
            _isDragging =
                false;

            _draggingLine =
                null;

            _draggingStartPoint =
                false;

            _draggingEndPoint =
                false;


            if (DiagramCanvas.IsMouseCaptured)
            {
                DiagramCanvas.ReleaseMouseCapture();
            }


            DrawAll();
        }


        // =========================================================
        // ПРЕДПРОСМОТР НОВОЙ ЛИНИИ
        // =========================================================

        private void DrawManualLinePreview(
            WpfPoint start,
            WpfPoint end)
        {
            Brush brush =
                GetComponentBrush(
                    GetSelectedComponent());


            var line =
                new Line
                {
                    X1 =
                        start.X,

                    Y1 =
                        start.Y,

                    X2 =
                        end.X,

                    Y2 =
                        end.Y,

                    Stroke =
                        brush,

                    StrokeThickness =
                        3
                };


            DiagramCanvas.Children.Add(
                line);


            AddMarker(
                start,
                brush);
        }


        // =========================================================
        // СОЗДАНИЕ НОВОЙ ЛИНИИ
        // =========================================================

        private void CompleteManualLine(
            WpfPoint start,
            WpfPoint end)
        {
            if (Distance(
                    start,
                    end) < 5)
            {
                DrawAll();

                return;
            }


            if (!TryParseDouble(
                    ValueTextBox.Text,
                    out double value))
            {
                MessageBox.Show(
                    "Введите корректное значение линии.",
                    "Добавление линии",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _drawingManualLine =
                    true;

                _manualLineStart =
                    start;

                return;
            }


            string component =
                GetSelectedComponent();


            if (string.IsNullOrWhiteSpace(
                    component))
            {
                MessageBox.Show(
                    "Выберите компонент линии.",
                    "Добавление линии",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            var model =
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
                        _image.Width,

                    Y1 =
                        start.Y /
                        _image.Height,

                    X2 =
                        end.X /
                        _image.Width,

                    Y2 =
                        end.Y /
                        _image.Height,

                    IsVerified =
                        true,

                    Confidence =
                        1.0
                };


            var viewModel =
                new DiagramGridLineViewModel(
                    model);


            _gridLines.Add(
                viewModel);


            LinesListBox.SelectedItem =
                viewModel;


            DrawAll();
        }


        // =========================================================
        // НАЧАТЬ РУЧНОЕ ДОБАВЛЕНИЕ
        // =========================================================

        private void AddManualLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _drawingManualLine =
                true;

            _manualLineStart =
                null;


            MessageBox.Show(
                "Нажмите на начало линии, затем нажмите на её конец.",
                "Ручное добавление линии",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


            DiagramCanvas.Focus();
        }


        // =========================================================
        // ПОЛУЧИТЬ КОМПОНЕНТ
        // =========================================================

        private string GetSelectedComponent()
        {
            if (ComponentComboBox.SelectedItem
                is ComboBoxItem item)
            {
                return item.Content?.ToString()
                       ?? string.Empty;
            }


            return string.Empty;
        }


        // =========================================================
        // ЦВЕТ КАТЕГОРИИ
        // =========================================================

        private static Brush GetComponentBrush(
            string component)
        {
            return component switch
            {
                "CaO" =>
                    Brushes.Red,

                "MgO" =>
                    Brushes.Gold,

                "SiO2" =>
                    Brushes.MediumPurple,

                _ =>
                    Brushes.DodgerBlue
            };
        }


        // =========================================================
        // РУЧНАЯ ИЗОЛИНИЯ
        // =========================================================

        private void AddManualIsolineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryParseDouble(
                    ViscosityTextBox.Text,
                    out double viscosity) ||
                viscosity <= 0)
            {
                MessageBox.Show(
                    "Введите корректное положительное значение вязкости.",
                    "Добавление изолинии",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ViscosityTextBox.Focus();

                return;
            }


            _drawingManualIsoline =
                true;

            _manualIsolinePoints.Clear();


            _manualIsolinePolyline =
                new Polyline
                {
                    Stroke =
                        Brushes.LimeGreen,

                    StrokeThickness =
                        3,

                    Fill =
                        Brushes.Transparent
                };


            DiagramCanvas.Children.Add(
                _manualIsolinePolyline);


            MessageBox.Show(
                "Последовательно нажимайте левой кнопкой мыши точки изолинии.\n\n" +
                "После завершения нажмите Enter.",
                "Ручное добавление изолинии",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


            DiagramCanvas.Focus();
        }


        private void AddManualIsolinePoint(
            WpfPoint point)
        {
            _manualIsolinePoints.Add(
                point);

            _manualIsolinePolyline?.Points.Add(
                point);
        }


        // =========================================================
        // ЗАВЕРШЕНИЕ ИЗОЛИНИИ
        // =========================================================

        private void FinishManualIsoline()
        {
            if (!_drawingManualIsoline)
                return;


            if (_manualIsolinePoints.Count < 2)
            {
                MessageBox.Show(
                    "Для изолинии необходимо указать минимум две точки.",
                    "Изолиния",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (!TryParseDouble(
                    ViscosityTextBox.Text,
                    out double viscosity) ||
                viscosity <= 0)
            {
                MessageBox.Show(
                    "Введите корректное положительное значение вязкости.",
                    "Изолиния",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            var points =
                _manualIsolinePoints
                    .Select(
                        p =>
                            new DiagramIsolinePoint(
                                p.X /
                                _image.Width,

                                p.Y /
                                _image.Height))
                    .ToList();


            var model =
                new DiagramViscosityIsoline
                {
                    DiagramId =
                        _diagramId,

                    Viscosity =
                        viscosity,

                    IsVerified =
                        true,

                    Confidence =
                        1.0
                };


            model.Points =
                points;


            var viewModel =
                new DiagramViscosityIsolineViewModel(
                    model);


            _isolines.Add(
                viewModel);


            IsolinesListBox.SelectedItem =
                viewModel;


            _drawingManualIsoline =
                false;

            _manualIsolinePoints.Clear();


            if (_manualIsolinePolyline != null)
            {
                DiagramCanvas.Children.Remove(
                    _manualIsolinePolyline);

                _manualIsolinePolyline =
                    null;
            }


            DrawAll();
        }


        // =========================================================
        // ПОДТВЕРДИТЬ ЛИНИЮ
        // =========================================================

        private void VerifyLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (LinesListBox.SelectedItem
                is not DiagramGridLineViewModel selected)
            {
                return;
            }


            selected.Model.IsVerified =
                true;


            RefreshGridList();

            DrawAll();
        }


        // =========================================================
        // ИЗМЕНИТЬ ЛИНИЮ
        // =========================================================

        private void UpdateLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (LinesListBox.SelectedItem
                is not DiagramGridLineViewModel selected)
            {
                MessageBox.Show(
                    "Выберите линию сетки.",
                    "Редактирование",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (!TryParseDouble(
                    ValueTextBox.Text,
                    out double value))
            {
                MessageBox.Show(
                    "Введите корректное значение.",
                    "Редактирование",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            string component =
                GetSelectedComponent();


            if (string.IsNullOrWhiteSpace(
                    component))
            {
                MessageBox.Show(
                    "Выберите компонент.",
                    "Редактирование",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            selected.Model.Component =
                component;

            selected.Model.Value =
                value;

            selected.Model.IsVerified =
                true;


            RefreshGridList();


            LinesListBox.SelectedItem =
                selected;


            DrawAll();
        }


        // =========================================================
        // ПОДТВЕРДИТЬ ИЗОЛИНИЮ
        // =========================================================

        private void VerifyIsolineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (IsolinesListBox.SelectedItem
                is not DiagramViscosityIsolineViewModel selected)
            {
                return;
            }


            if (selected.Model.Viscosity <= 0)
            {
                MessageBox.Show(
                    "Сначала укажите значение вязкости.",
                    "Изолиния",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            selected.Model.IsVerified =
                true;


            RefreshIsolineList();

            DrawAll();
        }


        // =========================================================
        // УДАЛЕНИЕ ЛИНИИ
        // =========================================================

        private void DeleteLineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (LinesListBox.SelectedItem
                is not DiagramGridLineViewModel selected)
            {
                return;
            }


            _gridLines.Remove(
                selected);

            DrawAll();
        }


        // =========================================================
        // УДАЛЕНИЕ ИЗОЛИНИИ
        // =========================================================

        private void DeleteIsolineButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (IsolinesListBox.SelectedItem
                is not DiagramViscosityIsolineViewModel selected)
            {
                return;
            }


            _isolines.Remove(
                selected);

            DrawAll();
        }


        // =========================================================
        // ВЫБОР ЛИНИИ В СПИСКЕ
        // =========================================================

        private void LinesListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (LinesListBox.SelectedItem
                is not DiagramGridLineViewModel selected)
            {
                return;
            }


            var model =
                selected.Model;


            ValueTextBox.Text =
                model.Value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);


            foreach (var item
                     in ComponentComboBox.Items)
            {
                if (item is ComboBoxItem comboItem &&
                    string.Equals(
                        comboItem.Content?.ToString(),
                        model.Component,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ComponentComboBox.SelectedItem =
                        comboItem;

                    break;
                }
            }


            DrawAll();
        }


        // =========================================================
        // ВЫБОР ИЗОЛИНИИ
        // =========================================================

        private void IsolinesListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IsolinesListBox.SelectedItem
                is not DiagramViscosityIsolineViewModel selected)
            {
                return;
            }


            ViscosityTextBox.Text =
                selected.Model.Viscosity.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);


            DrawAll();
        }


        // =========================================================
        // ОБЩАЯ ОТРИСОВКА
        // =========================================================

        private void DrawAll()
        {
            if (!IsLoaded)
                return;


            DiagramCanvas.Children.Clear();


            DrawGrid();

            DrawIsolines();


            if (_drawingManualIsoline &&
                _manualIsolinePolyline != null)
            {
                DiagramCanvas.Children.Add(
                    _manualIsolinePolyline);
            }
        }


        // =========================================================
        // ОТРИСОВКА СЕТКИ
        // =========================================================

        private void DrawGrid()
        {
            if (_image.Width <= 0 ||
                _image.Height <= 0)
            {
                return;
            }


            foreach (var item in _gridLines)
            {
                var model =
                    item.Model;


                var p1 =
                    GetPixelPoint(
                        model.X1,
                        model.Y1);


                var p2 =
                    GetPixelPoint(
                        model.X2,
                        model.Y2);


                bool selected =
                    LinesListBox.SelectedItem ==
                    item;


                Brush componentBrush =
                    GetComponentBrush(
                        model.Component);


                var line =
                    new Line
                    {
                        X1 =
                            p1.X,

                        Y1 =
                            p1.Y,

                        X2 =
                            p2.X,

                        Y2 =
                            p2.Y,

                        Stroke =
                            componentBrush,

                        StrokeThickness =
                            selected
                                ? 4
                                : 2
                    };


                DiagramCanvas.Children.Add(
                    line);


                /*
                 * Для выбранной линии показываем
                 * точки начала и конца.
                 *
                 * Именно эти точки можно перетаскивать.
                 */

                if (selected)
                {
                    AddMarker(
                        p1,
                        componentBrush);

                    AddMarker(
                        p2,
                        componentBrush);
                }
            }
        }


        // =========================================================
        // ОТРИСОВКА ИЗОЛИНИЙ
        // =========================================================

        private void DrawIsolines()
        {
            if (_image.Width <= 0 ||
                _image.Height <= 0)
            {
                return;
            }


            foreach (var item in _isolines)
            {
                var model =
                    item.Model;


                var points =
                    model.Points;


                if (points.Count < 2)
                    continue;


                bool selected =
                    IsolinesListBox.SelectedItem ==
                    item;


                var polyline =
                    new Polyline
                    {
                        Stroke =
                            Brushes.LimeGreen,

                        StrokeThickness =
                            selected
                                ? 4
                                : 3,

                        Fill =
                            Brushes.Transparent
                    };


                foreach (var point in points)
                {
                    polyline.Points.Add(
                        GetPixelPoint(
                            point.X,
                            point.Y));
                }


                DiagramCanvas.Children.Add(
                    polyline);
            }
        }


        // =========================================================
        // ПЕРЕВОД НОРМАЛИЗОВАННЫХ КООРДИНАТ В ПИКСЕЛИ
        // =========================================================

        private WpfPoint GetPixelPoint(
            double normalizedX,
            double normalizedY)
        {
            return new WpfPoint(
                normalizedX *
                _image.Width,

                normalizedY *
                _image.Height);
        }


        // =========================================================
        // МАРКЕР КОНЦА ЛИНИИ
        // =========================================================

        private void AddMarker(
            WpfPoint point,
            Brush brush)
        {
            var ellipse =
                new Ellipse
                {
                    Width =
                        12,

                    Height =
                        12,

                    Fill =
                        brush,

                    Stroke =
                        Brushes.White,

                    StrokeThickness =
                        2,

                    Cursor =
                        Cursors.SizeAll
                };


            Canvas.SetLeft(
                ellipse,
                point.X - 6);


            Canvas.SetTop(
                ellipse,
                point.Y - 6);


            DiagramCanvas.Children.Add(
                ellipse);
        }


        // =========================================================
        // ПРОВЕРКА БЛИЗОСТИ К ТОЧКЕ
        // =========================================================

        private static bool IsNearPoint(
            WpfPoint point,
            WpfPoint target,
            double tolerance)
        {
            return Distance(
                       point,
                       target)
                   <= tolerance;
        }


        // =========================================================
        // ВЫБОР ОБЪЕКТА ПО КЛИКУ
        // =========================================================

        private void SelectElementAtPoint(
            WpfPoint point)
        {
            const double tolerance = 10;


            // Сначала линии сетки.

            for (int i =
                     _gridLines.Count - 1;
                 i >= 0;
                 i--)
            {
                var model =
                    _gridLines[i].Model;


                var p1 =
                    GetPixelPoint(
                        model.X1,
                        model.Y1);


                var p2 =
                    GetPixelPoint(
                        model.X2,
                        model.Y2);


                if (DistancePointToSegment(
                        point,
                        p1,
                        p2) <= tolerance)
                {
                    LinesListBox.SelectedItem =
                        _gridLines[i];

                    return;
                }
            }


            // Затем изолинии.

            for (int i =
                     _isolines.Count - 1;
                 i >= 0;
                 i--)
            {
                var points =
                    _isolines[i]
                        .Model
                        .Points;


                for (int j = 1;
                     j < points.Count;
                     j++)
                {
                    var p1 =
                        GetPixelPoint(
                            points[j - 1].X,
                            points[j - 1].Y);


                    var p2 =
                        GetPixelPoint(
                            points[j].X,
                            points[j].Y);


                    if (DistancePointToSegment(
                            point,
                            p1,
                            p2) <= tolerance)
                    {
                        IsolinesListBox.SelectedItem =
                            _isolines[i];

                        return;
                    }
                }
            }
        }


        // =========================================================
        // РАССТОЯНИЕ ДО ОТРЕЗКА
        // =========================================================

        private static double DistancePointToSegment(
            WpfPoint p,
            WpfPoint a,
            WpfPoint b)
        {
            double dx =
                b.X - a.X;

            double dy =
                b.Y - a.Y;


            if (Math.Abs(dx) < 0.000001 &&
                Math.Abs(dy) < 0.000001)
            {
                return Distance(
                    p,
                    a);
            }


            double t =
                ((p.X - a.X) * dx +
                 (p.Y - a.Y) * dy) /
                (dx * dx +
                 dy * dy);


            t =
                Math.Max(
                    0,
                    Math.Min(
                        1,
                        t));


            var projection =
                new WpfPoint(
                    a.X + t * dx,
                    a.Y + t * dy);


            return Distance(
                p,
                projection);
        }


        private static double Distance(
            WpfPoint a,
            WpfPoint b)
        {
            double dx =
                a.X - b.X;

            double dy =
                a.Y - b.Y;


            return Math.Sqrt(
                dx * dx +
                dy * dy);
        }


        // =========================================================
        // ОБНОВЛЕНИЕ СПИСКА ЛИНИЙ
        // =========================================================

        private void RefreshGridList()
        {
            int selectedIndex =
                LinesListBox.SelectedIndex;


            LinesListBox.ItemsSource =
                null;


            LinesListBox.ItemsSource =
                _gridLines;


            if (selectedIndex >= 0 &&
                selectedIndex < _gridLines.Count)
            {
                LinesListBox.SelectedIndex =
                    selectedIndex;
            }
        }


        // =========================================================
        // ОБНОВЛЕНИЕ СПИСКА ИЗОЛИНИЙ
        // =========================================================

        private void RefreshIsolineList()
        {
            int selectedIndex =
                IsolinesListBox.SelectedIndex;


            IsolinesListBox.ItemsSource =
                null;


            IsolinesListBox.ItemsSource =
                _isolines;


            if (selectedIndex >= 0 &&
                selectedIndex < _isolines.Count)
            {
                IsolinesListBox.SelectedIndex =
                    selectedIndex;
            }
        }


        // =========================================================
        // СОХРАНЕНИЕ
        // =========================================================

        private void SaveAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string diagramName =
                DiagramNameTextBox.Text.Trim();


            if (string.IsNullOrWhiteSpace(
                    diagramName))
            {
                MessageBox.Show(
                    "Введите название диаграммы.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DiagramNameTextBox.Focus();

                return;
            }


            if (!TryParseDouble(
                    Al2O3TextBox.Text,
                    out double al2o3))
            {
                MessageBox.Show(
                    "Введите корректное значение Al₂O₃.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Al2O3TextBox.Focus();

                return;
            }


            if (al2o3 < 0 ||
                al2o3 > 100)
            {
                MessageBox.Show(
                    "Содержание Al₂O₃ должно находиться в диапазоне от 0 до 100 %.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Al2O3TextBox.Focus();

                return;
            }


            if (!TryParseDouble(
                    TemperatureTextBox.Text,
                    out double temperature))
            {
                MessageBox.Show(
                    "Введите корректную температуру.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TemperatureTextBox.Focus();

                return;
            }


            if (_gridLines.Any(
                    x => !x.Model.IsVerified))
            {
                MessageBox.Show(
                    "Не все линии сетки подтверждены.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (_isolines.Any(
                    x =>
                        !x.Model.IsVerified ||
                        x.Model.Viscosity <= 0))
            {
                MessageBox.Show(
                    "Не все изолинии вязкости подтверждены и имеют значение вязкости.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            try
            {
                _diagramService.UpdateDiagramInfo(
                    _diagramId,
                    diagramName,
                    al2o3,
                    temperature);


                _gridService.ReplaceLines(
                    _diagramId,
                    _gridLines.Select(
                        x => x.Model));


                _isolineService.ReplaceIsolines(
                    _diagramId,
                    _isolines.Select(
                        x => x.Model));


                MessageBox.Show(
                    "Диаграмма, сетка и изолинии успешно сохранены.",
                    "Сохранение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                DialogResult =
                    true;


                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка сохранения:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ENTER / ESCAPE
        // =========================================================

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                _drawingManualIsoline)
            {
                FinishManualIsoline();

                e.Handled =
                    true;

                return;
            }


            if (e.Key == Key.Escape)
            {
                if (_drawingManualIsoline)
                {
                    CancelManualIsoline();

                    e.Handled =
                        true;

                    return;
                }


                if (_drawingManualLine)
                {
                    _drawingManualLine =
                        false;

                    _manualLineStart =
                        null;

                    DrawAll();

                    e.Handled =
                        true;

                    return;
                }


                if (_isDragging)
                {
                    StopDragging();

                    e.Handled =
                        true;
                }
            }
        }


        // =========================================================
        // ОТМЕНА ИЗОЛИНИИ
        // =========================================================

        private void CancelManualIsoline()
        {
            _drawingManualIsoline =
                false;


            _manualIsolinePoints.Clear();


            if (_manualIsolinePolyline != null)
            {
                DiagramCanvas.Children.Remove(
                    _manualIsolinePolyline);

                _manualIsolinePolyline =
                    null;
            }


            DrawAll();
        }


        // =========================================================
        // ПАРСИНГ ЧИСЛА
        // =========================================================

        private static bool TryParseDouble(
            string text,
            out double value)
        {
            text =
                text.Trim();


            return double.TryParse(
                text.Replace(
                    ',',
                    '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}