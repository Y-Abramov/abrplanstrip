using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Geom
{
    internal enum FoldSide { Left, Right }

    internal sealed class FoldRange
    {
        public FoldSide Side;
        public double FromStation;
        public double ToStation;
        public double MaxAllowedWidth;   // минимальный радиус на участке
    }

    /// <summary>Складка полосы: на внутренней стороне кривой при смещении больше радиуса
    /// кривизны отображение перестаёт быть однозначным и полоса налезает сама на себя.
    /// Геометрию не правим - сообщаем пользователю конкретный участок и предельную ширину.</summary>
    internal static class FoldDetector
    {
        public static IList<FoldRange> Find(ICenterline line, StripFrame frame, double step)
        {
            var result = new List<FoldRange>();
            if (step <= 0.0) step = 5.0;

            FoldRange left = null, right = null;

            for (double s = frame.StartStation; s <= frame.EndStation + 1e-9; s += step)
            {
                double radius = line.CurvatureRadiusAt(s);
                if (double.IsInfinity(radius) || radius <= 0.0)
                {
                    Close(result, ref left);
                    Close(result, ref right);
                    continue;
                }

                // Внутренняя сторона - та, в которую поворачивает трасса.
                bool turnsLeft = line.TangentAt(Next(s, step, frame)) >= line.TangentAt(s);

                bool foldLeft = turnsLeft && frame.LeftWidth >= radius;
                bool foldRight = !turnsLeft && frame.RightWidth >= radius;

                Track(result, ref left, foldLeft, FoldSide.Left, s, radius);
                Track(result, ref right, foldRight, FoldSide.Right, s, radius);
            }

            Close(result, ref left);
            Close(result, ref right);
            return result;
        }

        private static double Next(double station, double step, StripFrame frame)
        {
            double next = station + step;
            return next > frame.EndStation ? frame.EndStation : next;
        }

        private static void Track(
            IList<FoldRange> result, ref FoldRange current, bool active,
            FoldSide side, double station, double radius)
        {
            if (active)
            {
                if (current == null)
                {
                    current = new FoldRange
                    {
                        Side = side,
                        FromStation = station,
                        ToStation = station,
                        MaxAllowedWidth = radius
                    };
                }
                else
                {
                    current.ToStation = station;
                    if (radius < current.MaxAllowedWidth) current.MaxAllowedWidth = radius;
                }
            }
            else
            {
                Close(result, ref current);
            }
        }

        private static void Close(IList<FoldRange> result, ref FoldRange current)
        {
            if (current == null) return;
            result.Add(current);
            current = null;
        }
    }
}
