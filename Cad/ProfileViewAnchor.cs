using AbrCivil.PlanStrip.Core.Geom;
using AbrCivil.PlanStrip.Core.Model;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Куда встаёт блок полосы: по горизонтали вид профиля 1:1 к пикетажу,
    /// по вертикали - над верхом сетки с зазором. Ось полосы в блоке проходит через 0,
    /// поэтому вставка поднимается ещё на правую ширину.</summary>
    internal static class ProfileViewAnchor
    {
        public static Point3d Compute(ProfileView view, StripSettings settings)
        {
            double top = view.ElevationMax;

            double x = 0.0, y = 0.0;
            view.FindXYAtStationAndElevation(view.StationStart, top, ref x, ref y);

            double lift = settings.GapAboveGrid + settings.RightWidth * settings.CrossScale;
            return new Point3d(x, y + lift, 0.0);
        }

        public static StripFrame Frame(ProfileView view, StripSettings settings)
        {
            return new StripFrame
            {
                StartStation = view.StationStart,
                EndStation = view.StationEnd,
                LeftWidth = settings.LeftWidth,
                RightWidth = settings.RightWidth,
                CrossScale = settings.CrossScale
            };
        }
    }
}
