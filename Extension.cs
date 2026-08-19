using System;
using System.Collections.Generic;
using Abr.Civil.Sdk;
using AbrCivil.PlanStrip.Cad;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Presets;
using AbrCivil.PlanStrip.Ui;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: ExtensionApplication(typeof(AbrCivil.PlanStrip.PlanStripExtension))]
[assembly: CommandClass(typeof(AbrCivil.PlanStrip.PlanStripExtension))]

namespace AbrCivil.PlanStrip
{
    public class PlanStripExtension : IExtensionApplication
    {
        public void Initialize()
        {
            AbrRibbon.WhenReady(BuildRibbon);
        }

        public void Terminate()
        {
        }

        private static void BuildRibbon()
        {
            var tab = AbrRibbon.EnsureTab();
            if (tab == null) return;

            var panel = AbrRibbon.EnsurePanel(tab, "ABR_CIVIL_PLANSTRIP_PANEL", "Развёртка плана");
            panel.Source.Items.Add(AbrRibbon.MakeButton("Развёртка", "PLANSTRIP", "planstrip_build"));
            panel.Source.Items.Add(AbrRibbon.MakeButton("Обновить", "PLANSTRIPUPDATE", "planstrip_update"));
            panel.Source.Items.Add(AbrRibbon.MakeButton("Удалить", "PLANSTRIPERASE", "planstrip_erase"));
            panel.Source.Items.Add(AbrRibbon.MakeButton("Наборы оформления", "PLANSTRIPSETTINGS", "planstrip_settings"));
            panel.Source.Items.Add(AbrRibbon.MakeButton("О модуле", "PLANSTRIPABOUT", "planstrip_about"));
        }

        [CommandMethod("PLANSTRIPABOUT")]
        public void About()
        {
            using (var dialog = new AboutDialog())
                dialog.ShowDialog();
        }

        [CommandMethod("PLANSTRIP")]
        public void Build()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            string report = null;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var alignmentNames = new List<string>();
                var civilDoc = CivilApplication.ActiveDocument;
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    var alignment = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                    if (alignment != null) alignmentNames.Add(alignment.Name);
                }

                if (alignmentNames.Count == 0)
                {
                    ed.WriteMessage("\nВ чертеже нет трасс.");
                    tr.Commit();
                    return;
                }

                var layerNames = CollectLayerNames(db, tr);

                Func<string, IList<KeyValuePair<string, string>>> profileViewsFor = name =>
                {
                    var list = new List<KeyValuePair<string, string>>();
                    var alignment = StripService.FindAlignment(db, tr, name);
                    if (alignment == null) return list;

                    foreach (ObjectId id in alignment.GetProfileViewIds())
                    {
                        var view = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
                        if (view != null) list.Add(new KeyValuePair<string, string>(view.Name, view.Handle.ToString()));
                    }
                    return list;
                };

                var presets = new StripPresetStore();

                using (var dialog = new StripDialog(alignmentNames, profileViewsFor, layerNames, presets, new StripSettings()))
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        tr.Commit();
                        return;
                    }

                    var view = StripService.FindProfileView(db, tr, dialog.ProfileViewHandle);
                    if (view == null)
                    {
                        ed.WriteMessage("\nВид профиля не найден.");
                        tr.Commit();
                        return;
                    }

                    var model = new StripModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        AlignmentName = dialog.AlignmentName,
                        ProfileViewHandle = dialog.ProfileViewHandle,
                        Settings = dialog.BuildSettings()
                    };
                    model.BlockName = StripService.MakeBlockName(model.AlignmentName, view.StationStart, view.StationEnd);

                    var result = StripService.Build(db, tr, model);
                    report = result.ToText();
                }

                tr.Commit();
            }

            if (report != null)
                using (var dialog = new ReportDialog(report))
                    dialog.ShowDialog();
        }

        [CommandMethod("PLANSTRIPUPDATE")]
        public void Update()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            var peo = new PromptEntityOptions("\nВыберите полосу для обновления (Enter - обновить все): ");
            peo.AllowNone = true;
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK && per.Status != PromptStatus.None) return;

            string report = null;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                if (per.Status == PromptStatus.None)
                {
                    var sb = new System.Text.StringBuilder();
                    var models = StripStore.LoadAll(db, tr);

                    if (models.Count == 0)
                    {
                        ed.WriteMessage("\nВ чертеже нет полос.");
                    }
                    else
                    {
                        foreach (var model in models)
                            sb.AppendLine(model.BlockName + ":").AppendLine(StripService.Build(db, tr, model).ToText());
                        report = sb.ToString();
                    }
                }
                else
                {
                    var model = ResolveSelectedModel(db, tr, per.ObjectId);
                    if (model == null)
                    {
                        ed.WriteMessage("\nВыбранный объект - не часть полосы.");
                    }
                    else
                    {
                        report = StripService.Build(db, tr, model).ToText();
                    }
                }

                tr.Commit();
            }

            if (report != null)
                using (var dialog = new ReportDialog(report))
                    dialog.ShowDialog();
        }

        [CommandMethod("PLANSTRIPERASE")]
        public void Erase()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            var per = ed.GetEntity("\nВыберите полосу для удаления: ");
            if (per.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var model = ResolveSelectedModel(db, tr, per.ObjectId);
                if (model == null)
                {
                    ed.WriteMessage("\nВыбранный объект - не часть полосы.");
                    tr.Commit();
                    return;
                }

                var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                string name = StripBlockBuilder.SafeName(model.BlockName);

                if (table.Has(name))
                {
                    var definition = (BlockTableRecord)tr.GetObject(table[name], OpenMode.ForWrite);

                    foreach (ObjectId refId in definition.GetBlockReferenceIds(true, true))
                    {
                        var reference = tr.GetObject(refId, OpenMode.ForWrite) as Entity;
                        if (reference != null && !reference.IsErased) reference.Erase();
                    }

                    definition.Erase();
                }

                StripStore.Remove(db, tr, model.Id);
                ed.WriteMessage("\nПолоса удалена: " + model.BlockName);
                tr.Commit();
            }
        }

        [CommandMethod("PLANSTRIPSETTINGS")]
        public void Settings()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            var pso = new PromptStringOptions("\nИмя набора оформления для сохранения: ") { AllowSpaces = true };
            var psr = ed.GetString(pso);
            if (psr.Status != PromptStatus.OK || string.IsNullOrEmpty(psr.StringResult)) return;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var alignmentNames = new List<string>();
                var civilDoc = CivilApplication.ActiveDocument;
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    var alignment = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                    if (alignment != null) alignmentNames.Add(alignment.Name);
                }

                var layerNames = CollectLayerNames(db, tr);

                Func<string, IList<KeyValuePair<string, string>>> profileViewsFor = name =>
                {
                    var list = new List<KeyValuePair<string, string>>();
                    var alignment = StripService.FindAlignment(db, tr, name);
                    if (alignment == null) return list;

                    foreach (ObjectId id in alignment.GetProfileViewIds())
                    {
                        var view = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
                        if (view != null) list.Add(new KeyValuePair<string, string>(view.Name, view.Handle.ToString()));
                    }
                    return list;
                };

                var presets = new StripPresetStore();

                using (var dialog = new StripDialog(alignmentNames, profileViewsFor, layerNames, presets, new StripSettings()))
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        dialog.SaveAsPreset(psr.StringResult);
                        ed.WriteMessage("\nНабор оформления сохранён: " + psr.StringResult);
                    }
                }

                tr.Commit();
            }
        }

        private static List<string> CollectLayerNames(Database db, Transaction tr)
        {
            var names = new List<string>();
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                var record = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                if (record != null) names.Add(record.Name);
            }
            return names;
        }

        /// <summary>Найти модель полосы, которой принадлежит выбранный объект: либо это
        /// сама вставка блока полосы, либо объект внутри её определения (в режиме правки блока).</summary>
        private static StripModel ResolveSelectedModel(Database db, Transaction tr, ObjectId id)
        {
            var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
            string blockName = null;

            var reference = entity as BlockReference;
            if (reference != null)
            {
                var definition = (BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                blockName = definition.Name;
            }
            else if (entity != null)
            {
                var owner = tr.GetObject(entity.OwnerId, OpenMode.ForRead) as BlockTableRecord;
                if (owner != null) blockName = owner.Name;
            }

            if (blockName == null) return null;

            foreach (var model in StripStore.LoadAll(db, tr))
                if (StripBlockBuilder.SafeName(model.BlockName) == blockName) return model;

            return null;
        }
    }
}
