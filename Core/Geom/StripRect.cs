using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Обрезка по полосе. Выполняется уже в координатах пикетаж/смещение,
    /// где полоса - осевой прямоугольник: для выпуклой области хватает
    /// Лианга-Барски (открытые ломаные) и Сазерленда-Ходжмена (замкнутые контуры).</summary>
    internal sealed class StripRect
    {
        private readonly double _xMin, _xMax, _yMin, _yMax;

        public StripRect(double xMin, double xMax, double yMin, double yMax)
        {
            _xMin = xMin; _xMax = xMax; _yMin = yMin; _yMax = yMax;
        }

        public bool Contains(Pt2 p)
        {
            return p.X >= _xMin - 1e-12 && p.X <= _xMax + 1e-12
                && p.Y >= _yMin - 1e-12 && p.Y <= _yMax + 1e-12;
        }

        /// <summary>Открытая ломаная: список кусков, попавших в полосу.</summary>
        public IList<Pt2[]> ClipOpen(IList<Pt2> points)
        {
            var pieces = new List<Pt2[]>();
            var current = new List<Pt2>();

            for (int i = 0; i + 1 < points.Count; i++)
            {
                Pt2 a = points[i], b = points[i + 1];

                Pt2 ca, cb;
                if (!ClipSegment(a, b, out ca, out cb))
                {
                    Flush(pieces, current);
                    continue;
                }

                if (current.Count == 0 || !Same(current[current.Count - 1], ca))
                {
                    Flush(pieces, current);
                    current.Add(ca);
                }

                current.Add(cb);

                if (!Same(cb, b)) Flush(pieces, current);
            }

            Flush(pieces, current);
            return pieces;
        }

        /// <summary>Замкнутый контур: Сазерленд-Ходжмен по четырём кромкам.</summary>
        public Pt2[] ClipRing(IList<Pt2> ring)
        {
            IList<Pt2> result = ring;
            result = ClipEdge(result, 0);   // x >= xMin
            result = ClipEdge(result, 1);   // x <= xMax
            result = ClipEdge(result, 2);   // y >= yMin
            result = ClipEdge(result, 3);   // y <= yMax

            var array = new Pt2[result.Count];
            result.CopyTo(array, 0);
            return array;
        }

        private IList<Pt2> ClipEdge(IList<Pt2> input, int edge)
        {
            var output = new List<Pt2>();
            if (input.Count == 0) return output;

            for (int i = 0; i < input.Count; i++)
            {
                Pt2 current = input[i];
                Pt2 previous = input[(i + input.Count - 1) % input.Count];

                bool currentIn = InsideEdge(current, edge);
                bool previousIn = InsideEdge(previous, edge);

                if (currentIn)
                {
                    if (!previousIn) output.Add(EdgeIntersect(previous, current, edge));
                    output.Add(current);
                }
                else if (previousIn)
                {
                    output.Add(EdgeIntersect(previous, current, edge));
                }
            }

            return output;
        }

        private bool InsideEdge(Pt2 p, int edge)
        {
            switch (edge)
            {
                case 0: return p.X >= _xMin;
                case 1: return p.X <= _xMax;
                case 2: return p.Y >= _yMin;
                default: return p.Y <= _yMax;
            }
        }

        private Pt2 EdgeIntersect(Pt2 a, Pt2 b, int edge)
        {
            double t;
            switch (edge)
            {
                case 0: t = (_xMin - a.X) / (b.X - a.X); break;
                case 1: t = (_xMax - a.X) / (b.X - a.X); break;
                case 2: t = (_yMin - a.Y) / (b.Y - a.Y); break;
                default: t = (_yMax - a.Y) / (b.Y - a.Y); break;
            }

            return new Pt2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        /// <summary>Лианг-Барски для одного отрезка.</summary>
        private bool ClipSegment(Pt2 a, Pt2 b, out Pt2 ca, out Pt2 cb)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t0 = 0.0, t1 = 1.0;

            double[] p = { -dx, dx, -dy, dy };
            double[] q = { a.X - _xMin, _xMax - a.X, a.Y - _yMin, _yMax - a.Y };

            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0.0)
                {
                    if (q[i] < 0.0) { ca = a; cb = b; return false; }
                    continue;
                }

                double r = q[i] / p[i];
                if (p[i] < 0.0) { if (r > t1) { ca = a; cb = b; return false; } if (r > t0) t0 = r; }
                else { if (r < t0) { ca = a; cb = b; return false; } if (r < t1) t1 = r; }
            }

            ca = new Pt2(a.X + t0 * dx, a.Y + t0 * dy);
            cb = new Pt2(a.X + t1 * dx, a.Y + t1 * dy);
            return true;
        }

        private static bool Same(Pt2 a, Pt2 b)
        {
            return System.Math.Abs(a.X - b.X) < 1e-9 && System.Math.Abs(a.Y - b.Y) < 1e-9;
        }

        private static void Flush(IList<Pt2[]> pieces, List<Pt2> current)
        {
            if (current.Count >= 2) pieces.Add(current.ToArray());
            current.Clear();
        }
    }
}
