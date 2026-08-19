using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Сбор объектов, попавших в полосу.
    /// Обход пространства модели, а НЕ SelectCrossingPolygon: тот ловит только
    /// объекты, попавшие в текущий вид экрана, и молча теряет остальное.
    ///
    /// Внутрь заходим у ЛЮБОЙ вставки - и внешней ссылки, и обычного блока.
    /// Блок как точка вставки - это потеря всей его геометрии: пользователь видит
    /// пустую полосу там, где в чертеже нарисован план.</summary>
    internal sealed class EntityHarvester
    {
        private const int MaxDepth = 8;

        private enum Overlap { Inside, Outside, Unknown }

        private readonly ICenterline _line;
        private readonly StripFrame _frame;
        private readonly StripSettings _settings;
        private readonly HarvestReport _report;
        private readonly IProgressSink _sink;
        private readonly PrimitiveConverter _primitives;
        private readonly CivilConverter _civil;

        public EntityHarvester(
            ICenterline line, StripFrame frame, StripSettings settings,
            HarvestReport report, IProgressSink sink)
        {
            _line = line;
            _frame = frame;
            _settings = settings;
            _report = report;
            _sink = sink;
            _primitives = new PrimitiveConverter(settings.SagTolerance, report);
            _civil = new CivilConverter(settings.SagTolerance, report);
        }

        public void Harvest(
            Database db, Transaction tr, IList<FlexShape> flex, IList<RigidShape> rigid)
        {
            var bounds = BandBounds.Compute(_line, _frame);
            var box = new Extents3d(
                new Point3d(bounds.MinX, bounds.MinY, 0.0),
                new Point3d(bounds.MaxX, bounds.MaxY, 0.0));

            var ms = (BlockTableRecord)tr.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

            int topLevelCount = 0;
            foreach (ObjectId unused in ms) topLevelCount++;

            _sink.Begin("Сбор объектов", topLevelCount);
            Walk(db, tr, ms, Matrix3d.Identity, null, box, flex, rigid, 0);
            _sink.End();
        }

        private void Walk(
            Database db, Transaction tr, BlockTableRecord btr, Matrix3d transform,
            string ownerLayer, Extents3d box,
            IList<FlexShape> flex, IList<RigidShape> rigid, int depth)
        {
            foreach (ObjectId id in btr)
            {
                if (depth == 0) _sink.Tick();

                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null) continue;
                if (!entity.Visible) continue;

                // Слой «0» внутри блока наследуется от вставки - иначе фильтр слоёв
                // и слои развёртки врут для всего содержимого блоков.
                string layer = entity.Layer;
                if (ownerLayer != null && layer == "0") layer = ownerLayer;

                if (!_settings.Filter.Accepts(layer)) continue;
                if (!LayerVisible(db, tr, entity.LayerId)) continue;

                var overlap = Test(entity, transform, box);
                if (overlap == Overlap.Outside) continue;

                if (_civil.IsCivil(entity))
                {
                    if (overlap == Overlap.Unknown)
                    {
                        _report.AddSkipped("нет габаритов: " + entity.GetType().Name);
                        continue;
                    }

                    _civil.Convert(entity, tr, transform, layer, flex, rigid);
                    continue;
                }

                var reference = entity as BlockReference;
                if (reference != null)
                {
                    var definition = (BlockTableRecord)tr.GetObject(
                        reference.BlockTableRecord, OpenMode.ForRead);

                    // Своя же полоса: без этого PLANSTRIPUPDATE затягивает развёртку в себя.
                    if (definition.Name.StartsWith(StripService.BlockNamePrefix, StringComparison.Ordinal))
                        continue;

                    if (depth < MaxDepth)
                    {
                        // Композиция: сначала вставка блока, потом накопленное преобразование.
                        Walk(db, tr, definition, transform * reference.BlockTransform,
                            layer, box, flex, rigid, depth + 1);
                        continue;
                    }

                    _report.AddSkipped("вложенность блоков глубже " + MaxDepth);
                    continue;
                }

                if (overlap == Overlap.Unknown)
                {
                    _report.AddSkipped("нет габаритов: " + entity.GetType().Name);
                    continue;
                }

                _primitives.Convert(entity, transform, layer, flex, rigid);
            }
        }

        private static bool LayerVisible(Database db, Transaction tr, ObjectId layerId)
        {
            var record = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
            if (record == null) return true;
            return !record.IsOff && !record.IsFrozen;
        }

        /// <summary>Габариты объекта против габаритов коридора. Unknown - габаритов нет
        /// (пустой текст, вырожденная геометрия): для вставки это не повод не заходить внутрь.</summary>
        private static Overlap Test(Entity entity, Matrix3d transform, Extents3d box)
        {
            try
            {
                var extents = entity.GeometricExtents;
                extents.TransformBy(transform);

                if (extents.MaxPoint.X < box.MinPoint.X || extents.MinPoint.X > box.MaxPoint.X) return Overlap.Outside;
                if (extents.MaxPoint.Y < box.MinPoint.Y || extents.MinPoint.Y > box.MaxPoint.Y) return Overlap.Outside;
                return Overlap.Inside;
            }
            catch (Exception)
            {
                return Overlap.Unknown;
            }
        }
    }
}
