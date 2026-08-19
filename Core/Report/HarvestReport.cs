using System.Collections.Generic;
using System.Text;
using AbrCivil.PlanStrip.Core.Geom;

namespace AbrCivil.PlanStrip.Core.Report
{
    /// <summary>Что попало в полосу, что нет и почему. Без него пользователь
    /// на пустой полосе не поймёт, виноват фильтр слоёв, ширина или неподдержанный тип.</summary>
    internal sealed class HarvestReport
    {
        public readonly Dictionary<string, int> Taken = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Skipped = new Dictionary<string, int>();
        public int ClippedCount;
        public readonly List<FoldRange> Folds = new List<FoldRange>();

        public void AddTaken(string type)
        {
            Taken[type] = Taken.ContainsKey(type) ? Taken[type] + 1 : 1;
        }

        public void AddSkipped(string reason)
        {
            Skipped[reason] = Skipped.ContainsKey(reason) ? Skipped[reason] + 1 : 1;
        }

        public string ToText()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Взято в полосу:");
            foreach (var pair in Taken) sb.AppendLine("  " + pair.Key + ": " + pair.Value);

            if (Skipped.Count > 0)
            {
                sb.AppendLine("Пропущено:");
                foreach (var pair in Skipped) sb.AppendLine("  " + pair.Key + ": " + pair.Value);
            }

            if (ClippedCount > 0) sb.AppendLine("Обрезано по границе полосы: " + ClippedCount);

            foreach (var fold in Folds)
            {
                sb.AppendLine("Складка полосы " + (fold.Side == FoldSide.Left ? "слева" : "справа")
                    + ": пикеты " + fold.FromStation.ToString("F0") + "..." + fold.ToStation.ToString("F0")
                    + ", уменьшите ширину до " + fold.MaxAllowedWidth.ToString("F0"));
            }

            return sb.ToString();
        }
    }
}
