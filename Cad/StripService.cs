using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Сценарий построения полосы: сбор, развёртка, обрезка, отрисовка.
    /// Одна точка входа и для первой сборки, и для обновления.</summary>
    internal static class StripService
    {
        public static HarvestReport Build(Database db, Transaction tr, StripModel model, IProgressSink sink)
        {
            var report = new HarvestReport();

            var alignment = FindAlignment(db, tr, model.AlignmentName);
            var view = FindProfileView(db, tr, model.ProfileViewHandle);
            if (alignment == null || view == null)
            {
                report.AddSkipped("не найдена трасса или вид профиля");
                return report;
            }

            var line = new CenterlineAdapter(alignment, tr);
            var frame = ProfileViewAnchor.Frame(view, model.Settings);

            model.StartStation = frame.StartStation;
            model.EndStation = frame.EndStation;

            var flex = new List<FlexShape>();
            var rigid = new List<RigidShape>();

            new EntityHarvester(line, frame, model.Settings, report, sink).Harvest(db, tr, flex, rigid);

            foreach (var fold in FoldDetector.Find(line, frame, 5.0)) report.Folds.Add(fold);

            var mapper = new ShapeMapper(line, frame, model.Settings);

            sink.Begin("Развёртка", flex.Count + rigid.Count);
            var mappedFlex = mapper.MapFlex(flex, report, sink);
            var mappedRigid = mapper.MapRigid(rigid, report, sink);
            sink.End();

            IList<double> seams = mapper.Seams;

            var decoration = new GostDecorator(db, tr, model.Settings).Build(line, frame, seams);

            StripBlockBuilder.Build(db, tr, model, mappedFlex, mappedRigid, decoration,
                ProfileViewAnchor.Compute(view, model.Settings), sink);

            StripStore.Save(db, tr, model);
            return report;
        }

        public static Alignment FindAlignment(Database db, Transaction tr, string name)
        {
            var doc = CivilApplication.ActiveDocument;
            foreach (ObjectId id in doc.GetAlignmentIds())
            {
                var alignment = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                if (alignment != null && alignment.Name == name) return alignment;
            }
            return null;
        }

        /// <summary>По этому префиксу сбор узнаёт собственные полосы и не тянет их в себя.</summary>
        public const string BlockNamePrefix = "ABR_Развёртка_";

        /// <summary>«ABR_Развёртка_Трасса1_0-1000» - имя чистится от запрещённых
        /// в блоках символов уже в StripBlockBuilder.SafeName при сборке.</summary>
        public static string MakeBlockName(string alignmentName, double startStation, double endStation)
        {
            return BlockNamePrefix + alignmentName + "_"
                + startStation.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                + "-" + endStation.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static ProfileView FindProfileView(Database db, Transaction tr, string handleText)
        {
            long handleValue;
            if (string.IsNullOrEmpty(handleText) || !long.TryParse(
                handleText, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out handleValue))
                return null;

            ObjectId id;
            if (!db.TryGetObjectId(new Handle(handleValue), out id)) return null;

            return tr.GetObject(id, OpenMode.ForRead) as ProfileView;
        }

    }
}
