using System;
using System.Globalization;
using System.Windows;
using OpenCvSharp;
using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;
using ViscosityDeterminator.Services;

namespace ViscosityDeterminator
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly DiagramService _diagramService;
        private readonly DiagramPointService _pointService;
        private readonly DiagramGridService _gridService;
        private readonly DiagramViscosityIsolineService _isolineService;
        private readonly ViscosityCalculationService _viscosityService;

        public MainWindow()
        {
            InitializeComponent();

            // =====================================================
            // Работа с диаграммами
            // =====================================================

            _diagramService = new DiagramService();

            _diagramService.InitializeDatabase();

            // =====================================================
            // Поиск точки состава
            // =====================================================

            _pointService =
                new DiagramPointService();

            // =====================================================
            // Работа с сохранённой сеткой диаграмм
            // =====================================================

            _gridService =
                new DiagramGridService(
                    new KnowledgeBaseContext());

            // =====================================================
            // Работа с сохранёнными изолиниями вязкости
            // =====================================================

            _isolineService =
                new DiagramViscosityIsolineService(
                    new KnowledgeBaseContext());

            // =====================================================
            // Расчёт вязкости
            // =====================================================

            _viscosityService =
                new ViscosityCalculationService();
        }

        // =========================================================
        // ДОБАВЛЕНИЕ ДИАГРАММЫ
        // =========================================================

        private void AddDiagramMenuItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window =
                new AddDiagramWindow
                {
                    Owner = this
                };

            window.ShowDialog();
        }

        // =========================================================
        // БАЗА ЗНАНИЙ
        // =========================================================

        private void OpenKnowledgeBaseMenuItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window =
                new KnowledgeBaseWindow
                {
                    Owner = this
                };

            window.ShowDialog();
        }

        // =========================================================
        // РАСЧЁТ ВЯЗКОСТИ
        // =========================================================

        private void CalculateViscosityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // -----------------------------------------------------
            // Чтение CaO
            // -----------------------------------------------------

            if (!TryReadValue(
                    CaOTextBox.Text,
                    "CaO",
                    out double cao))
            {
                return;
            }

            // -----------------------------------------------------
            // Чтение MgO
            // -----------------------------------------------------

            if (!TryReadValue(
                    MgOTextBox.Text,
                    "MgO",
                    out double mgo))
            {
                return;
            }

            // -----------------------------------------------------
            // Чтение Al2O3
            // -----------------------------------------------------

            if (!TryReadValue(
                    Al2O3TextBox.Text,
                    "Al₂O₃",
                    out double al2o3))
            {
                return;
            }

            // -----------------------------------------------------
            // Чтение SiO2
            // -----------------------------------------------------

            if (!TryReadValue(
                    SiO2TextBox.Text,
                    "SiO₂",
                    out double sio2))
            {
                return;
            }

            // -----------------------------------------------------
            // Чтение температуры
            // -----------------------------------------------------

            if (!TryReadValue(
                    TemperatureTextBox.Text,
                    "Температура",
                    out double temperature))
            {
                return;
            }

            // -----------------------------------------------------
            // Проверка отрицательных значений
            // -----------------------------------------------------

            if (cao < 0 ||
                mgo < 0 ||
                al2o3 < 0 ||
                sio2 < 0)
            {
                MessageBox.Show(
                    "Содержание компонентов не может быть отрицательным.",
                    "Ошибка ввода",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // -----------------------------------------------------
            // Проверка суммы компонентов
            // -----------------------------------------------------

            double sum =
                cao +
                mgo +
                al2o3 +
                sio2;

            if (Math.Abs(sum - 100.0) > 0.0001)
            {
                MessageBox.Show(
                    $"Сумма компонентов должна быть равна 100 %.\n\n" +
                    $"Текущая сумма: {sum:F2} %",
                    "Ошибка состава",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // -----------------------------------------------------
            // Проверка доступных диаграмм Al2O3
            // -----------------------------------------------------

            if (al2o3 != 5 &&
                al2o3 != 10 &&
                al2o3 != 15)
            {
                MessageBox.Show(
                    "Для расчёта доступны диаграммы с содержанием " +
                    "Al₂O₃ 5, 10 и 15 %.",
                    "Ошибка состава",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // -----------------------------------------------------
            // Проверка доступных температур
            // -----------------------------------------------------

            if (temperature != 1400 &&
                temperature != 1450 &&
                temperature != 1500)
            {
                MessageBox.Show(
                    "Для расчёта доступны температуры " +
                    "1400, 1450 и 1500 °C.",
                    "Ошибка температуры",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                // =================================================
                // ПОИСК НУЖНОЙ ДИАГРАММЫ
                // =================================================

                Diagram? diagram =
                    _diagramService.FindDiagram(
                        al2o3,
                        temperature);

                if (diagram == null)
                {
                    MessageBox.Show(
                        $"В базе знаний не найдена диаграмма:\n\n" +
                        $"Al₂O₃: {al2o3:F0} %\n" +
                        $"Температура: {temperature:F0} °C",
                        "Диаграмма не найдена",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // =================================================
                // ЗАГРУЗКА ИЗОБРАЖЕНИЯ ДИАГРАММЫ
                // =================================================

                using var sourceImage =
                    Cv2.ImDecode(
                        diagram.ImageData,
                        ImreadModes.Color);

                if (sourceImage.Empty())
                {
                    MessageBox.Show(
                        "Не удалось загрузить изображение найденной диаграммы.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                // =================================================
                // ПОЛУЧЕНИЕ СОХРАНЁННОЙ СЕТКИ
                // =================================================

                var gridLines =
                    _gridService.GetLines(
                        diagram.Id);

                if (gridLines.Count == 0)
                {
                    MessageBox.Show(
                        "Для выбранной диаграммы не сохранена сетка.\n\n" +
                        "Сначала необходимо открыть диаграмму в редакторе " +
                        "сетки и сохранить её.",
                        "Сетка отсутствует",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // =================================================
                // ПОИСК ТОЧКИ СОСТАВА
                // =================================================

                Point2f? point =
                    _pointService.FindPoint(
                        sourceImage,
                        gridLines,
                        cao,
                        mgo,
                        sio2);

                if (point == null)
                {
                    MessageBox.Show(
                        "Не удалось определить точку состава на диаграмме.\n\n" +
                        "Проверьте, что для данной диаграммы сохранены " +
                        "линии CaO, MgO и SiO₂ с необходимыми значениями.",
                        "Ошибка определения точки",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // =================================================
                // ПОЛУЧЕНИЕ СОХРАНЁННЫХ ИЗОЛИНИЙ
                // =================================================

                var isolines =
                    _isolineService.GetIsolines(
                        diagram.Id);

                if (isolines.Count == 0)
                {
                    MessageBox.Show(
                        "Для выбранной диаграммы не сохранены " +
                        "изолинии вязкости.\n\n" +
                        "Сначала необходимо открыть диаграмму в редакторе " +
                        "и подтвердить изолинии.",
                        "Изолинии отсутствуют",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // =================================================
                // РАСЧЁТ ВЯЗКОСТИ В НАЙДЕННОЙ ТОЧКЕ
                // =================================================

                ViscosityCalculationResult viscosityResult =
                    _viscosityService.Calculate(
                        point.Value,
                        isolines,
                        sourceImage.Width,
                        sourceImage.Height);

                if (!viscosityResult.Success)
                {
                    MessageBox.Show(
                        $"Не удалось определить вязкость.\n\n" +
                        viscosityResult.Message,
                        "Ошибка определения вязкости",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // =================================================
                // ОТРИСОВКА НАЙДЕННОЙ ТОЧКИ
                // =================================================

                using var resultImage =
                    _pointService.DrawPoint(
                        sourceImage,
                        point.Value);

                // =================================================
                // ПРЕОБРАЗОВАНИЕ ИЗОБРАЖЕНИЯ В PNG
                // =================================================

                Cv2.ImEncode(
                    ".png",
                    resultImage,
                    out var resultData);

                // =================================================
                // ПОКА ВЫВОДИМ РЕЗУЛЬТАТ РАСЧЁТА
                // =================================================

                string calculationType =
                    viscosityResult.IsExactIsolineMatch
                        ? "Точка находится на изолинии."
                        : "Вязкость определена интерполяцией между изолиниями.";

                MessageBox.Show(
                    $"Вязкость в выбранной точке:\n\n" +
                    $"{viscosityResult.Viscosity:F4} Па·с\n\n" +
                    $"{calculationType}",
                    "Результат расчёта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // =================================================
                // ОКНО РЕЗУЛЬТАТА
                // =================================================
                //
                // Текущий ViscosityResultWindow принимает только
                // изображение и исходные параметры.
                //
                // Поэтому пока открываем существующее окно после
                // расчёта. Само значение вязкости уже рассчитано
                // выше и будет подключено в это окно следующим шагом.
                // =================================================

                var resultWindow =
                    new ViscosityResultWindow(
                        resultData,
                        cao,
                        mgo,
                        al2o3,
                        sio2,
                        temperature)
                    {
                        Owner = this
                    };

                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при расчёте:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // ЧТЕНИЕ ЧИСЛОВОГО ЗНАЧЕНИЯ
        // =========================================================

        private bool TryReadValue(
            string text,
            string fieldName,
            out double value)
        {
            text = text.Trim();

            // Попытка чтения с текущей культурой Windows
            if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value))
            {
                return true;
            }

            // Попытка чтения с точкой
            if (double.TryParse(
                    text.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            MessageBox.Show(
                $"Введите корректное числовое значение " +
                $"для поля «{fieldName}».",
                "Ошибка ввода",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }
    }
}