namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Точка плана.</summary>
    internal struct Pt2
    {
        public readonly double X;
        public readonly double Y;

        public Pt2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return X.ToString("F3") + "; " + Y.ToString("F3");
        }
    }
}
