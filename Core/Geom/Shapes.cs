namespace AbrCivil.PlanStrip.Core.Geom
{
    internal enum FlexKind { Line, Ring, HatchLoop }

    /// <summary>Гибкий объект: ломаная, разворачивается по точкам.</summary>
    internal sealed class FlexShape
    {
        public Pt2[] Points;
        public bool Closed;
        public string Layer;
        public FlexKind Kind;
        public object Tag;
    }

    /// <summary>Жёсткий объект: точка вставки и поворот, размер не меняется.</summary>
    internal sealed class RigidShape
    {
        public Pt2 Insert;
        public double Rotation;
        public string Layer;
        public object Tag;
    }
}
