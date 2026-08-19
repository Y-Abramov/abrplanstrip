using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Примитивы AutoCAD в нормализованные формы.
    /// Гибкое - в ломаные по допуску стрелки, жёсткое - в точку вставки с поворотом.</summary>
    internal sealed class PrimitiveConverter
    {
        private readonly double _tolerance;
        private readonly HarvestReport _report;

        public PrimitiveConverter(double tolerance, HarvestReport report)
        {
            _tolerance = tolerance;
            _report = report;
        }

        public void Convert(
            Entity entity, Matrix3d transform, string layer,
            IList<FlexShape> flex, IList<RigidShape> rigid)
        {
            var text = entity as DBText;
            if (text != null)
            {
                rigid.Add(MakeRigid(text.Position.TransformBy(transform), text.Rotation, layer, entity));
                _report.AddTaken("Текст");
                return;
            }

            var mtext = entity as MText;
            if (mtext != null)
            {
                rigid.Add(MakeRigid(mtext.Location.TransformBy(transform), mtext.Rotation, layer, entity));
                _report.AddTaken("Многострочный текст");
                return;
            }

            var block = entity as BlockReference;
            if (block != null)
            {
                rigid.Add(MakeRigid(block.Position.TransformBy(transform), block.Rotation, layer, entity));
                _report.AddTaken("Блок");
                return;
            }

            var point = entity as DBPoint;
            if (point != null)
            {
                rigid.Add(MakeRigid(point.Position.TransformBy(transform), 0.0, layer, entity));
                _report.AddTaken("Точка");
                return;
            }

            var hatch = entity as Hatch;
            if (hatch != null)
            {
                ConvertHatch(hatch, transform, layer, flex);
                return;
            }

            if (entity is Xline || entity is Ray)
            {
                // Построительные прямая/луч: бесконечны, EndParam у них не реализован
                // (eNotImplementedYet) - для развёртки как источник геометрии бессмысленны.
                _report.AddSkipped("построительная линия/луч");
                return;
            }

            var curve = entity as Curve;
            if (curve != null)
            {
                var points = Sample(curve, transform);
                if (points.Count >= 2)
                {
                    flex.Add(new FlexShape
                    {
                        Points = points.ToArray(),
                        Closed = curve.Closed,
                        Layer = layer,
                        Kind = curve.Closed ? FlexKind.Ring : FlexKind.Line,
                        Tag = entity.ObjectId
                    });
                    _report.AddTaken(entity.GetType().Name);
                }
                return;
            }

            _report.AddSkipped("тип не поддержан: " + entity.GetType().Name);
        }

        private static RigidShape MakeRigid(Point3d position, double rotation, string layer, Entity source)
        {
            return new RigidShape
            {
                Insert = new Pt2(position.X, position.Y),
                Rotation = rotation,
                Layer = layer,
                Tag = source.ObjectId
            };
        }

        private void ConvertHatch(Hatch hatch, Matrix3d transform, string layer, IList<FlexShape> flex)
        {
            for (int i = 0; i < hatch.NumberOfLoops; i++)
            {
                var loop = hatch.GetLoopAt(i);
                var polyline = hatch.GetLoopAt(i).Polyline;
                if (polyline == null) continue;

                var points = new List<Pt2>();
                foreach (BulgeVertex vertex in polyline)
                {
                    var p = new Point3d(vertex.Vertex.X, vertex.Vertex.Y, 0.0).TransformBy(transform);
                    points.Add(new Pt2(p.X, p.Y));
                }

                if (points.Count < 3) continue;

                flex.Add(new FlexShape
                {
                    Points = points.ToArray(),
                    Closed = true,
                    Layer = layer,
                    Kind = FlexKind.HatchLoop,
                    Tag = hatch.ObjectId
                });
            }

            _report.AddTaken("Штриховка");
        }

        /// <summary>Выборка точек кривой. Прямые сегменты берутся вершинами,
        /// дуговые - по допуску стрелки, сплайны и эллипсы - через Curve3d.</summary>
        private List<Pt2> Sample(Curve curve, Matrix3d transform)
        {
            var points = new List<Pt2>();

            var line = curve as Line;
            if (line != null)
            {
                Add(points, line.StartPoint, transform);
                Add(points, line.EndPoint, transform);
                return points;
            }

            var arc = curve as Arc;
            if (arc != null)
            {
                double sweep = arc.EndAngle - arc.StartAngle;
                if (sweep < 0.0) sweep += 2.0 * Math.PI;

                var samples = Densifier.Arc(
                    new Pt2(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngle, sweep, _tolerance);

                foreach (var p in samples) Add(points, new Point3d(p.X, p.Y, 0.0), transform);
                return points;
            }

            var circle = curve as Circle;
            if (circle != null)
            {
                var samples = Densifier.Circle(
                    new Pt2(circle.Center.X, circle.Center.Y), circle.Radius, _tolerance);

                foreach (var p in samples) Add(points, new Point3d(p.X, p.Y, 0.0), transform);
                return points;
            }

            var polyline = curve as Polyline;
            if (polyline != null)
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    double bulge = polyline.GetBulgeAt(i);
                    Add(points, polyline.GetPoint3dAt(i), transform);

                    if (Math.Abs(bulge) < 1e-12) continue;
                    if (i + 1 >= polyline.NumberOfVertices && !polyline.Closed) continue;

                    // Вырожденный сегмент (совпадающие соседние вершины): GetSegmentType
                    // молча отдаёт Point даже при ненулевом bulge, а GetArcSegmentAt на
                    // таком индексе бросает eInvalidInput. Вершина уже добавлена выше -
                    // просто не досэмплируем дугу, которой физически нет.
                    if (polyline.GetSegmentType(i) != SegmentType.Arc) continue;

                    var segment = polyline.GetArcSegmentAt(i);
                    double sweep = 4.0 * Math.Atan(bulge);

                    var samples = Densifier.Arc(
                        new Pt2(segment.Center.X, segment.Center.Y), segment.Radius,
                        segment.StartAngle, sweep, _tolerance);

                    for (int k = 1; k < samples.Length - 1; k++)
                        Add(points, new Point3d(samples[k].X, samples[k].Y, 0.0), transform);
                }

                if (polyline.Closed && points.Count > 0) points.Add(points[0]);
                return points;
            }

            // Сплайны, эллипсы, 2d/3d-полилинии: универсальная выборка геометрии.
            try
            {
                var ge = curve.GetGeCurve();
                var interval = ge.GetInterval();
                var samples = ge.GetSamplePoints(interval.LowerBound, interval.UpperBound, _tolerance);
                foreach (var sample in samples) Add(points, sample.Point, transform);
            }
            catch (Exception)
            {
                // Резерв: равномерно по длине. Хуже по качеству, но лучше, чем потерять объект.
                // EndParam/GetDistanceAtParameter сами могут бросить eNotImplementedYet -
                // это уже не повод ронять весь сбор, объект просто пропускается.
                try
                {
                    double length = curve.GetDistanceAtParameter(curve.EndParam);
                    int count = Math.Max(2, (int)Math.Ceiling(length / Math.Max(_tolerance * 50.0, 0.5)));

                    for (int i = 0; i <= count; i++)
                        Add(points, curve.GetPointAtDist(length * i / count), transform);
                }
                catch (Exception)
                {
                    _report.AddSkipped("геометрия не читается: " + curve.GetType().Name);
                }
            }

            return points;
        }

        private static void Add(List<Pt2> points, Point3d point, Matrix3d transform)
        {
            var p = point.TransformBy(transform);
            points.Add(new Pt2(p.X, p.Y));
        }
    }
}
