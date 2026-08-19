namespace AbrCivil.PlanStrip.Core.Model
{
    internal enum StraightenMode { Smooth, Corners }

    internal sealed class StripSettings
    {
        public double LeftWidth = 50.0;
        public double RightWidth = 50.0;
        public StraightenMode Mode = StraightenMode.Smooth;
        public bool Clip = true;
        public double CrossScale = 1.0;

        /// <summary>Допуск стрелки при дискретизации кривых, единицы чертежа.</summary>
        public double SagTolerance = 0.02;

        /// <summary>Зазор между верхом сетки профиля и нижней границей полосы.</summary>
        public double GapAboveGrid = 15.0;

        public bool DrawAxis = true;
        public bool DrawStations = true;
        public bool DrawCorners = true;
        public bool DrawCivilLabels = true;
        public double StationLabelStep = 100.0;

        public string LayerPrefix = "Развёртка_";
        public SourceFilter Filter = new SourceFilter();

        public StripSettings Clone()
        {
            var copy = (StripSettings)MemberwiseClone();
            copy.Filter = Filter.Clone();
            return copy;
        }
    }
}
