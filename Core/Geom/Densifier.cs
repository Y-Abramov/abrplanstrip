using System;
using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Разбиение кривых в ломаные по допуску стрелки.
    /// Стрелка h связана с центральным углом θ и радиусом R как h = R(1 − cos(θ/2)).</summary>
    internal static class Densifier
    {
        public static double MaxSweep(double radius, double tolerance)
        {
            if (radius <= 0.0 || tolerance <= 0.0) return Math.PI;
            double ratio = 1.0 - tolerance / radius;
            if (ratio <= -1.0) return Math.PI;
            if (ratio >= 1.0) return Math.PI;

            double sweep = 2.0 * Math.Acos(ratio);
            return sweep > Math.PI ? Math.PI : sweep;
        }

        public static int SegmentCount(double radius, double sweep, double tolerance)
        {
            double step = MaxSweep(radius, tolerance);
            int count = (int)Math.Ceiling(Math.Abs(sweep) / step);
            return count < 1 ? 1 : count;
        }

        /// <summary>Дуга от startAngle на sweep радиан (знак sweep задаёт направление).</summary>
        public static Pt2[] Arc(Pt2 center, double radius, double startAngle, double sweep, double tolerance)
        {
            int count = SegmentCount(radius, sweep, tolerance);
            var points = new Pt2[count + 1];

            for (int i = 0; i <= count; i++)
            {
                double angle = startAngle + sweep * i / count;
                points[i] = new Pt2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle));
            }

            return points;
        }

        /// <summary>Замкнутая ломаная без повтора первой точки в конце.</summary>
        public static Pt2[] Circle(Pt2 center, double radius, double tolerance)
        {
            int count = SegmentCount(radius, 2.0 * Math.PI, tolerance);
            if (count < 3) count = 3;

            var points = new List<Pt2>(count);
            for (int i = 0; i < count; i++)
            {
                double angle = 2.0 * Math.PI * i / count;
                points.Add(new Pt2(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }

            return points.ToArray();
        }
    }
}
