using System.Collections.Generic;
using System.Text;
using AbrCivil.PlanStrip.Core.Model;
using Autodesk.AutoCAD.DatabaseServices;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Хранение моделей полос внутри чертежа.
    /// Словарь ABR_PLANSTRIP в NOD, по одному Xrecord на полосу.
    /// JSON режется на куски: длина одной строки в записи ограничена.</summary>
    internal static class StripStore
    {
        public const string DictName = "ABR_PLANSTRIP";
        private const int ChunkSize = 250;

        public static void Save(Database db, Transaction tr, StripModel model)
        {
            var root = EnsureDictionary(db, tr);
            var json = StripJson.Save(model);

            var values = new List<TypedValue>();
            for (int i = 0; i < json.Length; i += ChunkSize)
            {
                int len = System.Math.Min(ChunkSize, json.Length - i);
                values.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, len)));
            }

            var record = new Xrecord();
            record.Data = new ResultBuffer(values.ToArray());

            root.UpgradeOpen();
            root.SetAt(model.Id, record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        public static StripModel Load(Database db, Transaction tr, string id)
        {
            var root = FindDictionary(db, tr);
            if (root == null || !root.Contains(id)) return null;

            var record = tr.GetObject(root.GetAt(id), OpenMode.ForRead) as Xrecord;
            if (record == null || record.Data == null) return null;

            var sb = new StringBuilder();
            foreach (TypedValue value in record.Data)
                if (value.TypeCode == (short)DxfCode.Text) sb.Append((string)value.Value);

            return StripJson.Load(sb.ToString());
        }

        public static List<StripModel> LoadAll(Database db, Transaction tr)
        {
            var list = new List<StripModel>();

            var root = FindDictionary(db, tr);
            if (root == null) return list;

            foreach (DBDictionaryEntry entry in root)
            {
                var model = Load(db, tr, entry.Key);
                if (model != null) list.Add(model);
            }

            return list;
        }

        public static void Remove(Database db, Transaction tr, string id)
        {
            var root = FindDictionary(db, tr);
            if (root == null || !root.Contains(id)) return;

            root.UpgradeOpen();
            root.Remove(id);
        }

        private static DBDictionary FindDictionary(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(DictName)) return null;

            return (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);
        }

        private static DBDictionary EnsureDictionary(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);

            if (nod.Contains(DictName))
                return (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);

            nod.UpgradeOpen();

            var dict = new DBDictionary();
            nod.SetAt(DictName, dict);
            tr.AddNewlyCreatedDBObject(dict, true);

            return dict;
        }
    }
}
