namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Геометрия полосы в её собственных координатах: X от нуля вдоль пикетажа,
    /// Y - смещение (влево положительное). Точка вставки блока считается в Cad.</summary>
    internal sealed class StripFrame
    {
        public double StartStation;
        public double EndStation;
        public double LeftWidth = 50.0;
        public double RightWidth = 50.0;
        public double CrossScale = 1.0;

        public double Length { get { return EndStation - StartStation; } }
    }
}
