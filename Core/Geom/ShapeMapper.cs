using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Report;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Перенос собранных форм в координаты полосы.
    ///
    /// Три правила, без которых полоса выходит пустой:
    /// 1. Отрезок сгущается ПЕРЕД переносом. Прямая в чертеже вдоль кривой трассы
    ///    в развёртке прямой не остаётся, а по двум концам этого не видно - линия
    ///    длиной в километр приезжала хордой либо исчезала целиком.
    /// 2. Точка вне полосы не выбрасывает форму. Непереносимые точки рвут форму
    ///    на куски, остальное живёт: линия, проходящая через полосу насквозь,
    ///    обязана остаться, хотя оба её конца снаружи.
    /// 3. Границу режет StripRect, а не выборка точек. Обрезка - геометрия,
    ///    а не отбор вершин.</summary>
    internal sealed class ShapeMapper
    {
        /// <summary>Потолок точек на форму: перенос точки - вызов API трассы,
        /// без потолка одна длинная горизонталь съедает минуты.</summary>
        private const int MaxPointsPerShape = 4000;

        private readonly StripFrame _frame;
        private readonly StripSettings _settings;
        private readonly SmoothStraightener _smooth;
        private readonly CornerStraightener _corners;
        private readonly StripRect _strip;
        private readonly StripRect _world;
        private readonly double _step;

        public ShapeMapper(ICenterline line, StripFrame frame, StripSettings settings)
        {
            _frame = frame;
            _settings = settings;

            _smooth = new SmoothStraightener(line, frame);
            _corners = new CornerStraightener(line, frame);

            _strip = new StripRect(
                0.0, frame.Length,
                -frame.RightWidth * frame.CrossScale,
                frame.LeftWidth * frame.CrossScale);

            var bounds = BandBounds.Compute(line, frame);
            _world = new StripRect(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY);

            _step = Math.Max(0.5, frame.Length / 1000.0);
        }

        /// <summary>Пикеты стыков кусков для режима «по углам»; в плавном режиме пусто.</summary>
        public IList<double> Seams
        {
            get
            {
                return _settings.Mode == StraightenMode.Corners
                    ? _corners.SeamStations()
                    : new List<double>();
            }
        }

        public List<FlexShape> MapFlex(IList<FlexShape> source, HarvestReport report)
        {
            var result = new List<FlexShape>();

            foreach (var shape in source)
            {
                if (shape.Points == null || shape.Points.Length < 2) continue;

                int before = result.Count;
                bool ring = shape.Closed && shape.Kind != FlexKind.Line;

                if (ring)
                {
                    // Кольцо режется по габаритам коридора как контур - остаётся замкнутым.
                    var world = _world.ClipRing(shape.Points);
                    if (world.Length >= 3) MapPoints(shape, world, true, result, report);
                }
                else
                {
                    foreach (var piece in _world.ClipOpen(shape.Points))
                        MapPoints(shape, piece, false, result, report);
                }

                if (result.Count == before) report.AddSkipped("не попало в полосу: " + KindName(shape.Kind));
            }

            return result;
        }

        public List<RigidShape> MapRigid(IList<RigidShape> source, HarvestReport report)
        {
            var result = new List<RigidShape>();

            foreach (var shape in source)
            {
                Pt2 strip;
                double rotation;

                bool ok = _settings.Mode == StraightenMode.Corners
                    ? _corners.TryMapRigid(shape.Insert, shape.Rotation, out strip, out rotation)
                    : _smooth.TryMapRigid(shape.Insert, shape.Rotation, out strip, out rotation);

                if (!ok)
                {
                    report.AddSkipped("проекция на трассу не определена");
                    continue;
                }

                if (!_strip.Contains(strip))
                {
                    report.AddSkipped("не попало в полосу: вставка");
                    continue;
                }

                result.Add(new RigidShape
                {
                    Insert = strip,
                    Rotation = rotation,
                    Layer = shape.Layer,
                    Tag = shape.Tag
                });
            }

            return result;
        }

        private void MapPoints(
            FlexShape shape, Pt2[] world, bool ring, List<FlexShape> result, HarvestReport report)
        {
            var dense = Densify(world, ring);
            var runs = MapRuns(dense);

            bool whole = ring && runs.Count == 1 && runs[0].Length == dense.Count;

            if (whole)
            {
                AddRing(shape, runs[0], result, report);
                return;
            }

            foreach (var run in runs) AddOpen(shape, run, result, report);
        }

        private void AddRing(FlexShape shape, Pt2[] points, List<FlexShape> result, HarvestReport report)
        {
            if (!_settings.Clip)
            {
                if (AnyInside(points)) result.Add(Clone(shape, points, true));
                return;
            }

            var clipped = _strip.ClipRing(points);
            if (clipped.Length < 3) return;

            if (clipped.Length != points.Length) report.ClippedCount++;
            result.Add(Clone(shape, clipped, true));
        }

        private void AddOpen(FlexShape shape, Pt2[] points, List<FlexShape> result, HarvestReport report)
        {
            if (points.Length < 2) return;

            if (!_settings.Clip)
            {
                if (AnyInside(points)) result.Add(Clone(shape, points, false));
                return;
            }

            foreach (var piece in _strip.ClipOpen(points))
            {
                if (piece.Length != points.Length) report.ClippedCount++;
                result.Add(Clone(shape, piece, false));
            }
        }

        /// <summary>Куски подряд идущих перенесённых точек. Разрыв - точка,
        /// для которой проекция на трассу не определена (за торцом трассы).</summary>
        private List<Pt2[]> MapRuns(IList<Pt2> dense)
        {
            var runs = new List<Pt2[]>();
            var current = new List<Pt2>();

            foreach (var world in dense)
            {
                Pt2 strip;
                if (TryMap(world, out strip))
                {
                    current.Add(strip);
                    continue;
                }

                if (current.Count >= 2) runs.Add(current.ToArray());
                current.Clear();
            }

            if (current.Count >= 2) runs.Add(current.ToArray());
            return runs;
        }

        private bool TryMap(Pt2 world, out Pt2 strip)
        {
            return _settings.Mode == StraightenMode.Corners
                ? _corners.TryMap(world, out strip)
                : _smooth.TryMap(world, out strip);
        }

        private bool AnyInside(Pt2[] points)
        {
            foreach (var p in points)
                if (_strip.Contains(p)) return true;
            return false;
        }

        /// <summary>Сгущение ломаной шагом вдоль отрезков. wrap - замыкающий отрезок
        /// кольца. Шаг растягивается, если точек выходит больше потолка.</summary>
        private List<Pt2> Densify(IList<Pt2> points, bool wrap)
        {
            int segments = wrap ? points.Count : points.Count - 1;

            double total = 0.0;
            for (int i = 0; i < segments; i++)
                total += Distance(points[i], points[(i + 1) % points.Count]);

            double step = _step;
            int budget = MaxPointsPerShape - points.Count;
            if (budget < 1) budget = 1;
            if (total / step > budget) step = total / budget;

            var dense = new List<Pt2>(points.Count + (int)(total / step) + 2);

            for (int i = 0; i < segments; i++)
            {
                Pt2 a = points[i], b = points[(i + 1) % points.Count];
                dense.Add(a);

                int pieces = (int)Math.Ceiling(Distance(a, b) / step);
                for (int k = 1; k < pieces; k++)
                {
                    double t = (double)k / pieces;
                    dense.Add(new Pt2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
            }

            if (!wrap) dense.Add(points[points.Count - 1]);
            return dense;
        }

        private static double Distance(Pt2 a, Pt2 b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static FlexShape Clone(FlexShape source, Pt2[] points, bool closed)
        {
            return new FlexShape
            {
                Points = points,
                Closed = closed,
                Layer = source.Layer,
                Kind = source.Kind,
                Tag = source.Tag
            };
        }

        private static string KindName(FlexKind kind)
        {
            switch (kind)
            {
                case FlexKind.Ring: return "контур";
                case FlexKind.HatchLoop: return "штриховка";
                default: return "линия";
            }
        }
    }
}
