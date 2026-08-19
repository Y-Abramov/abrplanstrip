using System;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Прямоугольник в координатах чертежа.</summary>
    internal struct Bounds2
    {
        public double MinX, MinY, MaxX, MaxY;

        public bool IsEmpty { get { return MinX > MaxX || MinY > MaxY; } }
    }

    /// <summary>Габариты коридора полосы в координатах чертежа: грубый предфильтр
    /// сбора и граница, за которой объект заведомо не попадёт в полосу.</summary>
    internal static class BandBounds
    {
        public static Bounds2 Compute(ICenterline line, StripFrame frame)
        {
            double step = Math.Max(1.0, frame.Length / 500.0);

            var bounds = new Bounds2
            {
                MinX = double.MaxValue,
                MinY = double.MaxValue,
                MaxX = double.MinValue,
                MaxY = double.MinValue
            };

            for (double s = frame.StartStation; s <= frame.EndStation + 1e-9; s += step)
                Sample(line, frame, s, ref bounds);

            Sample(line, frame, frame.EndStation, ref bounds);

            if (bounds.IsEmpty)
            {
                // Ось не опрашивается - лучше собрать лишнее, чем не собрать ничего.
                bounds.MinX = bounds.MinY = double.MinValue / 4.0;
                bounds.MaxX = bounds.MaxY = double.MaxValue / 4.0;
                return bounds;
            }

            // Запас на кромке: точка ровно на границе не должна теряться на округлении.
            double margin = Math.Max(1.0, (frame.LeftWidth + frame.RightWidth) * 0.05);
            bounds.MinX -= margin;
            bounds.MinY -= margin;
            bounds.MaxX += margin;
            bounds.MaxY += margin;

            return bounds;
        }

        private static void Sample(ICenterline line, StripFrame frame, double station, ref Bounds2 bounds)
        {
            try
            {
                Add(line.PointAt(station, frame.LeftWidth), ref bounds);
                Add(line.PointAt(station, -frame.RightWidth), ref bounds);
            }
            catch (Exception)
            {
                // Пикет вне трассы: пропускаем точку, а не роняем весь расчёт.
            }
        }

        private static void Add(Pt2 p, ref Bounds2 bounds)
        {
            if (p.X < bounds.MinX) bounds.MinX = p.X;
            if (p.X > bounds.MaxX) bounds.MaxX = p.X;
            if (p.Y < bounds.MinY) bounds.MinY = p.Y;
            if (p.Y > bounds.MaxY) bounds.MaxY = p.Y;
        }
    }
}
