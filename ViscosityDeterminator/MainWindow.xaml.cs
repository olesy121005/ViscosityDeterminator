using System;
using System.Globalization;
using System.Windows;
using OpenCvSharp;
using ViscosityDeterminator.Models;
using ViscosityDeterminator.Services;

namespace ViscosityDeterminator
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly DiagramService _diagramService;
        private readonly DiagramPointService _pointService;

        public MainWindow()
        {
            InitializeComponent();

            _diagramService = new DiagramService();
            _diagramService.InitializeDatabase();

            _pointService = new DiagramPointService();
        }

        private void AddDiagramMenuItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window = new AddDiagramWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void OpenKnowledgeBaseMenuItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            var window = new KnowledgeBaseWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void CalculateViscosityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryReadValue(
                    CaOTextBox.Text,
                    "CaO",
                    out double cao))
                return;

            if (!TryReadValue(
                    MgOTextBox.Text,
                    "MgO",
                    out double mgo))
                return;

            if (!TryReadValue(
                    Al2O3TextBox.Text,
                    "Al₂O₃",
                    out double al2o3))
                return;

            if (!TryReadValue(
                    SiO2TextBox.Text,
                    "SiO₂",
                    out double sio2))
                return;

            if (!TryReadValue(
                    TemperatureTextBox.Text,
                    "Температура",
                    out double temperature))
                return;

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

                Point2f? point =
    _pointService.FindPoint(
        sourceImage,
        cao,
        mgo,
        al2o3,
        sio2);

                if (point == null)
                {
                    MessageBox.Show(
                        "Не удалось определить точку состава на диаграмме.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                using var resultImage =
                    _pointService.DrawPoint(
                        sourceImage,
                        point.Value);

                Cv2.ImEncode(
                    ".png",
                    resultImage,
                    out var resultData);

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

        private bool TryReadValue(
            string text,
            string fieldName,
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

            MessageBox.Show(
                $"Введите корректное числовое значение для поля «{fieldName}».",
                "Ошибка ввода",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }
    }
}