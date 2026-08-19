using AbrCivil.PlanStrip.Core.Model;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Создание слоёв полосы по настройкам оформления и слоёв источников.
    /// Существующий слой не трогаем: правки пользователя должны пережить пересчёт.</summary>
    internal static class LayerFactory
    {
        public static ObjectId Ensure(Database db, Transaction tr, LayerStyle style)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(style.LayerName)) return table[style.LayerName];

            table.UpgradeOpen();

            var record = new LayerTableRecord();
            record.Name = style.LayerName;
            record.Color = Color.FromColorIndex(ColorMethod.ByAci, style.ColorIndex);

            if (style.LineWeight >= 0)
                record.LineWeight = (LineWeight)style.LineWeight;

            var linetypeId = EnsureLinetype(db, tr, style.Linetype);
            if (!linetypeId.IsNull) record.LinetypeObjectId = linetypeId;

            var id = table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);

            return id;
        }

        /// <summary>Слой источника. Имя слоя xref содержит «|», недопустимый в именах
        /// слоёв чертежа, - заменяем на «$0$», как это делает штатное связывание xref.</summary>
        public static ObjectId EnsureSource(Database db, Transaction tr, string layerName)
        {
            string name = layerName.Replace("|", "$0$");

            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(name)) return table[name];

            table.UpgradeOpen();

            var record = new LayerTableRecord { Name = name };
            var id = table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
            return id;
        }

        /// <summary>Тип линии из таблицы чертежа; если его нет - подгружаем из acadiso.lin.
        /// Не получилось - возвращаем Null, слой останется со сплошной линией.</summary>
        private static ObjectId EnsureLinetype(Database db, Transaction tr, string name)
        {
            if (string.IsNullOrEmpty(name)) return ObjectId.Null;

            var table = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (table.Has(name)) return table[name];

            try
            {
                db.LoadLineTypeFile(name, "acadiso.lin");

                var reloaded = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (reloaded.Has(name)) return reloaded[name];
            }
            catch (System.Exception)
            {
                // Нет такого типа линии в библиотеке - работаем сплошной.
            }

            return ObjectId.Null;
        }
    }
}
