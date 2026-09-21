using ViscosityDeterminator.Models;

namespace ViscosityDeterminator
{
    public class DiagramViscosityIsolineViewModel
    {
        public DiagramViscosityIsoline Model { get; }

        public string DisplayText
        {
            get
            {
                string verified =
                    Model.IsVerified
                        ? "✓"
                        : "○";

                string value =
                    Model.Viscosity > 0
                        ? $"{Model.Viscosity:0.###} Па·с"
                        : "значение не задано";

                return
                    $"{verified}  {value}";
            }
        }

        public DiagramViscosityIsolineViewModel(
            DiagramViscosityIsoline model)
        {
            Model = model;
        }
    }
}