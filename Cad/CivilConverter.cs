using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Объекты Civil 3D в нормализованные формы.
    /// Конкурент этого не делает вообще («объекты Сивила не обрабатываются») - здесь
    /// главное отличие модуля, поэтому каждый тип обрабатывается явно, а Explode -
    /// только резерв для того, что не покрыто стратегией.
    ///
    /// По спайку Task 2 (docs/superpowers/spikes/2026-08-13-planstrip-explode-probe.md):
    /// Explode для Civil-объектов почти всегда даёт пусто либо один BlockReference-прокси -
    /// не годится как источник геометрии, ЗА ИСКЛЮЧЕНИЕМ Parcel/ParcelSegment, где Explode
    /// возвращает настоящую Polyline (плюс подпись площади MText у Parcel) - для них
    /// отдельная стратегия не нужна, общий резерв ниже справляется сам.
    ///
    /// Метки (*LabelGroup - живой тип, не голый "Label") явной стратегии текста не имеют:
    /// спайк не вскрыл API для чтения текста/точки/поворота конкретной метки из LabelGroup,
    /// Explode для них даёт только BlockReference-прокси без текста. Ветка меток здесь падает
    /// в общий Explode-резерв как временное решение - RigidShape встанет на место метки,
    /// но текста в нём не будет. Первая живая проверка при работе над этим - Task 14.</summary>
    internal sealed class CivilConverter
    {
        private readonly double _tolerance;
        private readonly HarvestReport _report;

        public CivilConverter(double tolerance, HarvestReport report)
        {
            _tolerance = tolerance;
            _report = report;
        }

        public bool IsCivil(Entity entity)
        {
            return entity is CogoPoint
                || entity is TinSurface
                || entity is Pipe
                || entity is Structure
                || entity is FeatureLine
                || entity is Parcel
                || entity is Label;
        }

        public void Convert(
            Entity entity, Transaction tr, Matrix3d transform, string layer,
            IList<FlexShape> flex, IList<RigidShape> rigid)
        {
            var point = entity as CogoPoint;
            if (point != null)
            {
                var p = point.Location.TransformBy(transform);
                rigid.Add(new RigidShape
                {
                    Insert = new Pt2(p.X, p.Y),
                    Rotation = 0.0,
                    Layer = layer,
                    Tag = entity.ObjectId
                });
                _report.AddTaken("Точка COGO");
                return;
            }

            var surface = entity as TinSurface;
            if (surface != null)
            {
                ConvertContours(surface, tr, transform, layer, flex);
                return;
            }

            var pipe = entity as Pipe;
            if (pipe != null)
            {
                var a = pipe.StartPoint.TransformBy(transform);
                var b = pipe.EndPoint.TransformBy(transform);

                flex.Add(new FlexShape
                {
                    Points = new[] { new Pt2(a.X, a.Y), new Pt2(b.X, b.Y) },
                    Closed = false,
                    Layer = layer,
                    Kind = FlexKind.Line,
                    Tag = entity.ObjectId
                });
                _report.AddTaken("Труба");
                return;
            }

            var structure = entity as Structure;
            if (structure != null)
            {
                var p = structure.Location.TransformBy(transform);
                rigid.Add(new RigidShape
                {
                    Insert = new Pt2(p.X, p.Y),
                    Rotation = structure.Rotation,
                    Layer = layer,
                    Tag = entity.ObjectId
                });
                _report.AddTaken("Колодец");
                return;
            }

            var feature = entity as FeatureLine;
            if (feature != null)
            {
                var points = new List<Pt2>();
                foreach (Point3d p in feature.GetPoints(FeatureLinePointType.AllPoints))
                {
                    var t = p.TransformBy(transform);
                    points.Add(new Pt2(t.X, t.Y));
                }

                if (points.Count >= 2)
                {
                    flex.Add(new FlexShape
                    {
                        Points = points.ToArray(),
                        Closed = false,
                        Layer = layer,
                        Kind = FlexKind.Line,
                        Tag = entity.ObjectId
                    });
                    _report.AddTaken("Структурная линия");
                }
                return;
            }

            ConvertByExplode(entity, transform, layer, flex, rigid);
        }

        /// <summary>Горизонтали поверхности. ExtractContours создаёт объекты в чертеже -
        /// после конвертации их обязательно удаляем, иначе они там и останутся.</summary>
        private void ConvertContours(
            TinSurface surface, Transaction tr, Matrix3d transform, string layer, IList<FlexShape> flex)
        {
            ObjectIdCollection created = null;
            try
            {
                // Однопараметровая перегрузка сама берёт весь диапазон высот поверхности -
                // TinSurface не даёт GetMinimumElevation/GetMaximumElevation напрямую
                // (это свойства GeneralSurfaceProperties, отдельного объекта), а весь
                // диапазон и не нужен: ровно это делает ExtractContours(interval).
                created = surface.ExtractContours(1.0);

                foreach (ObjectId id in created)
                {
                    var curve = tr.GetObject(id, OpenMode.ForRead) as Curve;
                    if (curve == null) continue;

                    var points = new List<Pt2>();
                    double length = curve.GetDistanceAtParameter(curve.EndParam);
                    int count = Math.Max(2, (int)Math.Ceiling(length / Math.Max(_tolerance * 50.0, 0.5)));

                    for (int i = 0; i <= count; i++)
                    {
                        var p = curve.GetPointAtDist(length * i / count).TransformBy(transform);
                        points.Add(new Pt2(p.X, p.Y));
                    }

                    flex.Add(new FlexShape
                    {
                        Points = points.ToArray(),
                        Closed = curve.Closed,
                        Layer = layer,
                        Kind = FlexKind.Line,
                        Tag = surface.ObjectId
                    });
                }

                _report.AddTaken("Горизонтали поверхности");
            }
            catch (Exception ex)
            {
                _report.AddSkipped("поверхность не разобрана: " + ex.GetType().Name);
            }
            finally
            {
                if (created != null)
                {
                    foreach (ObjectId id in created)
                    {
                        var obj = tr.GetObject(id, OpenMode.ForWrite, false, true);
                        if (obj != null && !obj.IsErased) obj.Erase();
                    }
                }
            }
        }

        private void ConvertByExplode(
            Entity entity, Matrix3d transform, string layer,
            IList<FlexShape> flex, IList<RigidShape> rigid)
        {
            try
            {
                using (var parts = new DBObjectCollection())
                {
                    entity.Explode(parts);
                    if (parts.Count == 0)
                    {
                        _report.AddSkipped("пустой разбор: " + entity.GetType().Name);
                        return;
                    }

                    var converter = new PrimitiveConverter(_tolerance, _report);
                    foreach (DBObject part in parts)
                    {
                        var partEntity = part as Entity;
                        if (partEntity != null)
                            converter.Convert(partEntity, transform, layer, flex, rigid);

                        part.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _report.AddSkipped("не разбирается (" + entity.GetType().Name + "): " + ex.GetType().Name);
            }
        }
    }
}
