#if NET48
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif

namespace AbrCivil.PlanStrip.Core.Model
{
    /// <summary>JSON модели полосы. Формат плоский, поля - публичные свойства DTO.
    /// StripModel/StripSettings хранят данные в полях (не свойствах) - System.Text.Json
    /// молча теряет поля при сериализации, поэтому напрямую их сериализовать нельзя.</summary>
    internal static class StripJson
    {
        public sealed class Dto
        {
            public string Id { get; set; }
            public string AlignmentName { get; set; }
            public string ProfileViewHandle { get; set; }
            public string BlockName { get; set; }
            public double StartStation { get; set; }
            public double EndStation { get; set; }
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
            public System.Collections.Generic.List<string> Excluded { get; set; }
        }

        public static string Save(StripModel model)
        {
            var dto = new Dto
            {
                Id = model.Id,
                AlignmentName = model.AlignmentName,
                ProfileViewHandle = model.ProfileViewHandle,
                BlockName = model.BlockName,
                StartStation = model.StartStation,
                EndStation = model.EndStation,
                LeftWidth = model.Settings.LeftWidth,
                RightWidth = model.Settings.RightWidth,
                Mode = (int)model.Settings.Mode,
                Clip = model.Settings.Clip,
                CrossScale = model.Settings.CrossScale,
                SagTolerance = model.Settings.SagTolerance,
                GapAboveGrid = model.Settings.GapAboveGrid,
                DrawAxis = model.Settings.DrawAxis,
                DrawStations = model.Settings.DrawStations,
                DrawCorners = model.Settings.DrawCorners,
                DrawCivilLabels = model.Settings.DrawCivilLabels,
                StationLabelStep = model.Settings.StationLabelStep,
                LayerPrefix = model.Settings.LayerPrefix,
                Excluded = model.Settings.Filter.Excluded
            };

#if NET48
            return new JavaScriptSerializer().Serialize(dto);
#else
            return JsonSerializer.Serialize(dto);
#endif
        }

        public static StripModel Load(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

#if NET48
            var dto = new JavaScriptSerializer().Deserialize<Dto>(json);
#else
            var options = new JsonSerializerOptions();
            var dto = JsonSerializer.Deserialize<Dto>(json, options);
#endif
            if (dto == null) return null;

            var model = new StripModel
            {
                Id = dto.Id,
                AlignmentName = dto.AlignmentName,
                ProfileViewHandle = dto.ProfileViewHandle,
                BlockName = dto.BlockName,
                StartStation = dto.StartStation,
                EndStation = dto.EndStation
            };

            model.Settings.LeftWidth = dto.LeftWidth;
            model.Settings.RightWidth = dto.RightWidth;
            model.Settings.Mode = (StraightenMode)dto.Mode;
            model.Settings.Clip = dto.Clip;
            model.Settings.CrossScale = dto.CrossScale;
            model.Settings.SagTolerance = dto.SagTolerance;
            model.Settings.GapAboveGrid = dto.GapAboveGrid;
            model.Settings.DrawAxis = dto.DrawAxis;
            model.Settings.DrawStations = dto.DrawStations;
            model.Settings.DrawCorners = dto.DrawCorners;
            model.Settings.DrawCivilLabels = dto.DrawCivilLabels;
            model.Settings.StationLabelStep = dto.StationLabelStep;
            model.Settings.LayerPrefix = dto.LayerPrefix;
            if (dto.Excluded != null) model.Settings.Filter.Excluded = dto.Excluded;

            return model;
        }
    }
}
