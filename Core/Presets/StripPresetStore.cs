using System;
using System.Collections.Generic;
using System.IO;
using AbrCivil.PlanStrip.Core.Model;

#if NET48
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif

namespace AbrCivil.PlanStrip.Core.Presets
{
    /// <summary>Именованные пресеты настроек полосы в профиле пользователя.
    /// Один пресет - один файл, чтобы его можно было просто передать коллеге.</summary>
    internal sealed class StripPresetStore
    {
        private readonly string _dir;

        public StripPresetStore() : this(StripPaths.PresetsDir) { }

        public StripPresetStore(string directory)
        {
            _dir = directory;
        }

        public List<string> List()
        {
            var names = new List<string>();
            if (!Directory.Exists(_dir)) return names;

            foreach (var file in Directory.GetFiles(_dir, "*.json"))
            {
                var dto = ReadFile(file);
                if (dto != null && !string.IsNullOrEmpty(dto.Name)) names.Add(dto.Name);
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public StripSettings Load(string name)
        {
            string file = PathFor(name);
            if (!File.Exists(file)) return null;

            var dto = ReadFile(file);
            return dto != null ? dto.ToSettings() : null;
        }

        public void Save(string name, StripSettings settings)
        {
            Directory.CreateDirectory(_dir);
            var dto = Dto.From(name, settings);
            File.WriteAllText(PathFor(name), Serialize(dto), System.Text.Encoding.UTF8);
        }

        public void Delete(string name)
        {
            string file = PathFor(name);
            if (File.Exists(file)) File.Delete(file);
        }

        private string PathFor(string name)
        {
            return Path.Combine(_dir, SafeFileName(name) + ".json");
        }

        internal static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "preset";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';

            return new string(chars).Trim();
        }

        private static Dto ReadFile(string file)
        {
            try
            {
                return Deserialize(File.ReadAllText(file, System.Text.Encoding.UTF8));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Serialize(Dto dto)
        {
#if NET48
            return new JavaScriptSerializer().Serialize(dto);
#else
            return JsonSerializer.Serialize(dto);
#endif
        }

        private static Dto Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

#if NET48
            return new JavaScriptSerializer().Deserialize<Dto>(json);
#else
            return JsonSerializer.Deserialize<Dto>(json);
#endif
        }

        /// <summary>Плоский DTO с публичными свойствами - System.Text.Json/JavaScriptSerializer
        /// молча теряют поля StripSettings при прямой сериализации.</summary>
        private sealed class Dto
        {
            public string Name { get; set; }
            public double LeftWidth { get; set; }
            public double RightWidth { get; set; }
            public int Mode { get; set; }
            public bool Clip { get; set; }
            public double CrossScale { get; set; }
            public double SagTolerance { get; set; }
            public double GapAboveGrid { get; set; }
            public bool DrawAxis { get; set; }
            public bool DrawStations { get; set; }
            public bool DrawCorners { get; set; }
            public bool DrawCivilLabels { get; set; }
            public double StationLabelStep { get; set; }
            public string LayerPrefix { get; set; }
            public List<string> Excluded { get; set; }

            public static Dto From(string name, StripSettings s)
            {
                return new Dto
                {
                    Name = name,
                    LeftWidth = s.LeftWidth,
                    RightWidth = s.RightWidth,
                    Mode = (int)s.Mode,
                    Clip = s.Clip,
                    CrossScale = s.CrossScale,
                    SagTolerance = s.SagTolerance,
                    GapAboveGrid = s.GapAboveGrid,
                    DrawAxis = s.DrawAxis,
                    DrawStations = s.DrawStations,
                    DrawCorners = s.DrawCorners,
                    DrawCivilLabels = s.DrawCivilLabels,
                    StationLabelStep = s.StationLabelStep,
                    LayerPrefix = s.LayerPrefix,
                    Excluded = s.Filter.Excluded
                };
            }

            public StripSettings ToSettings()
            {
                var settings = new StripSettings
                {
                    LeftWidth = LeftWidth,
                    RightWidth = RightWidth,
                    Mode = (StraightenMode)Mode,
                    Clip = Clip,
                    CrossScale = CrossScale,
                    SagTolerance = SagTolerance,
                    GapAboveGrid = GapAboveGrid,
                    DrawAxis = DrawAxis,
                    DrawStations = DrawStations,
                    DrawCorners = DrawCorners,
                    DrawCivilLabels = DrawCivilLabels,
                    StationLabelStep = StationLabelStep,
                    LayerPrefix = LayerPrefix
                };
                if (Excluded != null) settings.Filter.Excluded = Excluded;
                return settings;
            }
        }
    }
}
