using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>ICenterline поверх трассы Civil 3D.
    /// StationOffsetAcceptOutOfRange - обязателен: обычный StationOffset бросает
    /// исключение на точке за пределами трассы, а у торцов полосы такие точки есть всегда.
    /// Сигнатуры сверены живьём по Civil3D_API_2026_Full.md (Task 9):
    /// GetDirectionAtStation возвращает Vector3d, не азимут-double; пятый параметр
    /// StationOffsetAcceptOutOfRange - ref bool (признак "в диапазоне"), не входной флаг;
    /// StartStation/EndStation объявлены на AlignmentCurve, не на базовом AlignmentEntity.</summary>
    internal sealed class CenterlineAdapter : ICenterline
    {
        private readonly Alignment _alignment;
        private readonly List<CornerRef> _corners;

        public CenterlineAdapter(Alignment alignment, Transaction tr)
        {
            _alignment = alignment;
            _corners = BuildCorners(alignment);
        }

        public double StartStation { get { return _alignment.StartingStation; } }
        public double EndStation { get { return _alignment.EndingStation; } }
        public IList<CornerRef> Corners { get { return _corners; } }

        public double TangentAt(double station)
        {
            // Alignment.GetDirectionAtStation из документации Civil3D_API не существует
            // (Alignment не наследует BaseBaseline - проверено живьём на 2026-08-13, дерево
            // наследования DBObject->Entity->Curve->...->Feature->Alignment). Азимут берём
            // из выходного параметра PointLocation(station, offset, tolerance, ...): по
            // соглашению Civil 3D bearing - радианы от севера по часовой, тот же перевод
            // в математический угол, что и предполагался для GetDirectionAtStation.
            double easting = 0.0, northing = 0.0, bearing = 0.0;
            _alignment.PointLocation(station, 0.0, 0.01, ref easting, ref northing, ref bearing);
            return Math.PI / 2.0 - bearing;
        }

        public double CurvatureRadiusAt(double station)
        {
            // Радиус берётся у элемента трассы, накрывающего пикет.
            var entities = _alignment.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var curveEntity = entities.GetEntityByOrder(i) as AlignmentCurve;
                if (curveEntity == null) continue;

                if (station < curveEntity.StartStation - 1e-6 || station > curveEntity.EndStation + 1e-6) continue;

                var arc = curveEntity as AlignmentArc;
                if (arc != null) return arc.Radius;

                var spiral = curveEntity as AlignmentSpiral;
                if (spiral != null) return SpiralRadius(spiral, station);

                return double.PositiveInfinity;
            }

            return double.PositiveInfinity;
        }

        public Pt2 PointAt(double station, double offset)
        {
            double easting = 0.0, northing = 0.0;
            _alignment.PointLocation(station, offset, ref easting, ref northing);
            return new Pt2(easting, northing);
        }

        public bool TryStationOffset(Pt2 point, out double station, out double offset)
        {
            station = 0.0;
            offset = 0.0;

            try
            {
                // Пятый параметр - признак «точка ВНЕ диапазона трассы» (outofrange).
                // В этом случае Civil возвращает станцию торца, а вместо смещения -
                // РАССТОЯНИЕ до точки торца (всегда положительное). Брать это за
                // смещение нельзя: объект уезжает в случайное место полосы.
                double s = 0.0, o = 0.0;
                bool outOfRange = false;
                _alignment.StationOffsetAcceptOutOfRange(point.X, point.Y, ref s, ref o, ref outOfRange);

                if (outOfRange) return false;

                station = s;
                offset = o;
                return !double.IsNaN(s) && !double.IsNaN(o);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static double SpiralRadius(AlignmentSpiral spiral, double station)
        {
            // Переходная кривая: радиус меняется линейно по длине. Для детектора складок
            // достаточно линейной интерполяции между радиусами концов.
            double t = (station - spiral.StartStation) / (spiral.EndStation - spiral.StartStation);
            double r1 = spiral.RadiusIn, r2 = spiral.RadiusOut;
            if (double.IsInfinity(r1) && double.IsInfinity(r2)) return double.PositiveInfinity;
            if (double.IsInfinity(r1)) return r2 / Math.Max(t, 1e-6);
            if (double.IsInfinity(r2)) return r1 / Math.Max(1.0 - t, 1e-6);
            return r1 + (r2 - r1) * t;
        }

        private static List<CornerRef> BuildCorners(Alignment alignment)
        {
            var corners = new List<CornerRef>();
            var entities = alignment.Entities;

            for (int i = 0; i < entities.Count; i++)
            {
                var arc = entities.GetEntityByOrder(i) as AlignmentArc;
                if (arc == null) continue;

                double deflection = arc.Delta;                   // радианы
                double radius = arc.Radius;
                double tangent = radius * Math.Tan(Math.Abs(deflection) / 2.0);
                double bisector = radius * (1.0 / Math.Cos(Math.Abs(deflection) / 2.0) - 1.0);

                corners.Add(new CornerRef
                {
                    Station = (arc.StartStation + arc.EndStation) / 2.0,
                    Deflection = arc.Clockwise ? -deflection : deflection,
                    Radius = radius,
                    Tangent = tangent,
                    CurveLength = arc.Length,
                    Bisector = bisector
                });
            }

            return corners;
        }
    }
}
