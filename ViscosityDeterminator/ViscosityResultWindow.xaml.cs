using OpenCvSharp;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ViscosityDeterminator
{
    public partial class ViscosityResultWindow : System.Windows.Window
    {
        public ViscosityResultWindow(
            byte[] imageData,
            double cao,
            double mgo,
            double al2o3,
            double sio2,
            double temperature)
        {
            InitializeComponent();

            ResultTextBlock.Text =
                $"CaO: {cao:F2} %    " +
                $"MgO: {mgo:F2} %    " +
                $"Al₂O₃: {al2o3:F2} %    " +
                $"SiO₂: {sio2:F2} %    " +
                $"T: {temperature:F0} °C";

            using var image =
                Cv2.ImDecode(
                    imageData,
                    ImreadModes.Color);

            if (!image.Empty())
            {
                ResultImage.Source =
                    ConvertMatToBitmapImage(image);
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
    }
}