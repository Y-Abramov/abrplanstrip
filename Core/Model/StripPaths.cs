using System;
using System.IO;

namespace AbrCivil.PlanStrip.Core.Model
{
    /// <summary>Пути данных модуля. Тот же корень, что у остальной линейки ABR | CIVIL.</summary>
    internal static class StripPaths
    {
        public static string DataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ABR", "Civil", "PlanStrip");
            }
        }

        public static string PresetsDir
        {
            get { return Path.Combine(DataRoot, "presets"); }
        }

        public static void EnsureDataRoot()
        {
            Directory.CreateDirectory(DataRoot);
        }
    }
}
