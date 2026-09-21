namespace ViscosityDeterminator.Models
{
    public class ViscosityCalculationResult
    {
        public bool Success { get; set; }

        public double Viscosity { get; set; }

        public string Message { get; set; } = string.Empty;

        public int? LowerIsolineId { get; set; }

        public int? UpperIsolineId { get; set; }

        public double? LowerViscosity { get; set; }

        public double? UpperViscosity { get; set; }

        public double? DistanceToLowerIsoline { get; set; }

        public double? DistanceToUpperIsoline { get; set; }

        public bool IsExactIsolineMatch { get; set; }

        public static ViscosityCalculationResult Failed(
            string message)
        {
            return new ViscosityCalculationResult
            {
                Success = false,
                Message = message
            };
        }

        public static ViscosityCalculationResult Exact(
            double viscosity,
            int isolineId,
            double distance)
        {
            return new ViscosityCalculationResult
            {
                Success = true,
                Viscosity = viscosity,
                Message =
                    $"Точка находится на изолинии {viscosity:0.###} Па·с.",
                LowerIsolineId = isolineId,
                UpperIsolineId = isolineId,
                LowerViscosity = viscosity,
                UpperViscosity = viscosity,
                DistanceToLowerIsoline = distance,
                DistanceToUpperIsoline = distance,
                IsExactIsolineMatch = true
            };
        }

        public static ViscosityCalculationResult Interpolated(
            double viscosity,
            int lowerIsolineId,
            double lowerViscosity,
            double lowerDistance,
            int upperIsolineId,
            double upperViscosity,
            double upperDistance)
        {
            return new ViscosityCalculationResult
            {
                Success = true,
                Viscosity = viscosity,

                Message =
                    $"Вязкость определена интерполяцией " +
                    $"между изолиниями " +
                    $"{lowerViscosity:0.###} и " +
                    $"{upperViscosity:0.###} Па·с.",

                LowerIsolineId =
                    lowerIsolineId,

                UpperIsolineId =
                    upperIsolineId,

                LowerViscosity =
                    lowerViscosity,

                UpperViscosity =
                    upperViscosity,

                DistanceToLowerIsoline =
                    lowerDistance,

                DistanceToUpperIsoline =
                    upperDistance,

                IsExactIsolineMatch = false
            };
        }
    }
}