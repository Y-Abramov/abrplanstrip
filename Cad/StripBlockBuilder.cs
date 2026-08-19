using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Сборка определения блока полосы и его вставки.
    /// Обновление = пересборка содержимого определения: вставка не трогается,
    /// поэтому сдвинутая пользователем полоса остаётся там, куда он её положил.
    /// Жёсткие формы (rigid) сюда не кладутся - код появляется вместе с Task 13,
    /// когда известна высота текста подписей из диалога.</summary>
    internal static class StripBlockBuilder
    {
        public static ObjectId Build(
            Database db, Transaction tr, StripModel model,
            IList<FlexShape> flex, IList<RigidShape> rigid,
            IList<Entity> decoration, Point3d insertPoint, IProgressSink sink)
        {
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            string name = SafeName(model.BlockName);

            BlockTableRecord definition;
            if (table.Has(name))
            {
                definition = (BlockTableRecord)tr.GetObject(table[name], OpenMode.ForWrite);
                ClearContent(tr, definition);
            }
            else
            {
                table.UpgradeOpen();
                definition = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
                table.Add(definition);
                tr.AddNewlyCreatedDBObject(definition, true);
            }

            sink.Begin("Отрисовка", flex.Count + decoration.Count);

            foreach (var shape in flex)
            {
                sink.Tick();

                var polyline = new Polyline();
                for (int i = 0; i < shape.Points.Length; i++)
                    polyline.AddVertexAt(i, new Point2d(shape.Points[i].X, shape.Points[i].Y), 0.0, 0.0, 0.0);

                polyline.Closed = shape.Closed;
                polyline.LayerId = LayerFactory.EnsureSource(db, tr, shape.Layer);

                definition.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
            }

            foreach (var entity in decoration)
            {
                sink.Tick();

                definition.AppendEntity(entity);
                tr.AddNewlyCreatedDBObject(entity, true);
            }

            sink.End();

            return EnsureReference(db, tr, definition, name, insertPoint);
        }

        private static ObjectId EnsureReference(
            Database db, Transaction tr, BlockTableRecord definition, string name, Point3d insertPoint)
        {
            var ms = (BlockTableRecord)tr.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var existing = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (existing == null) continue;

                var existingDefinition = (BlockTableRecord)tr.GetObject(
                    existing.BlockTableRecord, OpenMode.ForRead);

                if (existingDefinition.Name == name) return existing.ObjectId;   // вставка уже есть
            }

            ms.UpgradeOpen();

            var reference = new BlockReference(insertPoint, definition.ObjectId);
            ms.AppendEntity(reference);
            tr.AddNewlyCreatedDBObject(reference, true);
            return reference.ObjectId;
        }

        private static void ClearContent(Transaction tr, BlockTableRecord definition)
        {
            foreach (ObjectId id in definition)
            {
                var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (entity != null && !entity.IsErased) entity.Erase();
            }
        }

        /// <summary>В именах блоков запрещены < > / \ " : ; ? * | , = `</summary>
        public static string SafeName(string name)
        {
            var forbidden = new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' };
            var chars = name.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(forbidden, chars[i]) >= 0) chars[i] = '_';

            return new string(chars);
        }
    }
}
