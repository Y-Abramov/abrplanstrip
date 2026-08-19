using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Вершина угла поворота: нужна режиму «по углам» и подписям элементов кривых.</summary>
    internal sealed class CornerRef
    {
        public double Station;        // пикет вершины
        public double Deflection;     // угол поворота, радианы; >0 влево
        public double Radius;
        public double Tangent;
        public double CurveLength;
        public double Bisector;
    }

    /// <summary>Трасса глазами ядра. Реализация над Civil-объектом Alignment живёт в Cad.</summary>
    internal interface ICenterline
    {
        double StartStation { get; }
        double EndStation { get; }
        IList<CornerRef> Corners { get; }

        /// <summary>Азимут касательной в радианах, направление роста пикетажа.</summary>
        double TangentAt(double station);

        /// <summary>Радиус кривизны; на прямой - PositiveInfinity.</summary>
        double CurvatureRadiusAt(double station);

        Pt2 PointAt(double station, double offset);

        /// <summary>false, если проекция точки на трассу не определена.</summary>
        bool TryStationOffset(Pt2 point, out double station, out double offset);
    }
}
