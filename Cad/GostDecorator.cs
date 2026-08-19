using System;
using System.Collections.Generic;
using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Model;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Собственная графика полосы: ось, границы, пикетаж, углы поворота, стыки.
    /// Всё в координатах полосы (X от нуля вдоль пикетажа, Y - смещение).</summary>
    internal sealed class GostDecorator
    {
        private readonly Database _db;
        private readonly Transaction _tr;
        private readonly StripSettings _settings;

        public GostDecorator(Database db, Transaction tr, StripSettings settings)
        {
            _db = db;
            _tr = tr;
            _settings = settings;
        }

        public IList<Entity> Build(ICenterline line, StripFrame frame, IList<double> seams)
        {
            var result = new List<Entity>();

            if (_settings.DrawAxis)
            {
                result.Add(Line(new Point3d(0.0, 0.0, 0.0),
                                new Point3d(frame.Length, 0.0, 0.0), "Ось", 7));

                double top = frame.LeftWidth * frame.CrossScale;
                double bottom = -frame.RightWidth * frame.CrossScale;

                result.Add(Line(new Point3d(0.0, top, 0.0), new Point3d(frame.Length, top, 0.0), "Границы", 8));
                result.Add(Line(new Point3d(0.0, bottom, 0.0), new Point3d(frame.Length, bottom, 0.0), "Границы", 8));
            }

            if (_settings.DrawStations) AddStations(result, frame);
            if (_settings.DrawCorners) AddCorners(result, line, frame);

            foreach (double seam in seams)
            {
                double x = seam - frame.StartStation;
                result.Add(Line(
                    new Point3d(x, -frame.RightWidth * frame.CrossScale, 0.0),
                    new Point3d(x, frame.LeftWidth * frame.CrossScale, 0.0), "Стыки", 1));
            }

            return result;
        }

        private void AddStations(IList<Entity> result, StripFrame frame)
        {
            double step = _settings.StationLabelStep > 0.0 ? _settings.StationLabelStep : 100.0;
            double first = Math.Ceiling(frame.StartStation / step) * step;

            for (double s = first; s <= frame.EndStation + 1e-9; s += step)
            {
                double x = s - frame.StartStation;
                bool kilometre = Math.Abs(s % 1000.0) < 1e-6;
                double tick = kilometre ? 4.0 : 2.0;

                result.Add(Line(new Point3d(x, -tick, 0.0), new Point3d(x, tick, 0.0), "Пикетаж", 7));

                string text = kilometre
                    ? "КМ " + (s / 1000.0).ToString("F0")
                    : "ПК " + Math.Floor(s / 100.0).ToString("F0") + "+" + (s % 100.0).ToString("F0");

                result.Add(Text(new Point3d(x, -tick - 3.0, 0.0), text, "Пикетаж", 7));
            }
        }

        private void AddCorners(IList<Entity> result, ICenterline line, StripFrame frame)
        {
            foreach (var corner in line.Corners)
            {
                if (corner.Station < frame.StartStation || corner.Station > frame.EndStation) continue;

                double x = corner.Station - frame.StartStation;
                double top = frame.LeftWidth * frame.CrossScale;

                result.Add(Line(new Point3d(x, 0.0, 0.0), new Point3d(x, top, 0.0), "УглыПоворота", 3));

                string side = corner.Deflection >= 0.0 ? "влево" : "вправо";
                string text =
                    "Угол " + side + " " + ToDegrees(Math.Abs(corner.Deflection))
                    + "  R=" + corner.Radius.ToString("F0")
                    + "  T=" + corner.Tangent.ToString("F2")
                    + "  K=" + corner.CurveLength.ToString("F2")
                    + "  Б=" + corner.Bisector.ToString("F2");

                result.Add(Text(new Point3d(x, top + 2.0, 0.0), text, "УглыПоворота", 3));
            }
        }

        private static string ToDegrees(double radians)
        {
            double total = radians * 180.0 / Math.PI;
            int degrees = (int)total;
            int minutes = (int)Math.Round((total - degrees) * 60.0);
            if (minutes == 60) { degrees++; minutes = 0; }
            return degrees + "°" + minutes.ToString("00") + "'";
        }

        private Line Line(Point3d from, Point3d to, string layerSuffix, short color)
        {
            var line = new Line(from, to);
            line.LayerId = EnsureLayer(layerSuffix, color);
            return line;
        }

        private DBText Text(Point3d position, string value, string layerSuffix, short color)
        {
            var text = new DBText
            {
                Position = position,
                TextString = value,
                Height = 2.5
            };
            text.LayerId = EnsureLayer(layerSuffix, color);
            return text;
        }

        private ObjectId EnsureLayer(string suffix, short color)
        {
            var style = new LayerStyle
            {
                LayerName = _settings.LayerPrefix + suffix,
                ColorIndex = color,
                LineWeight = -3,
                Linetype = null
            };
            return LayerFactory.Ensure(_db, _tr, style);
        }
    }
}
