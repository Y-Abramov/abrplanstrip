using System;
using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Model
{
    /// <summary>Фильтр слоёв. По умолчанию берётся всё; пользователь снимает галки.
    /// Слои xref приходят как «имяссылки|имяслоя» - совпадение проверяем и по полному
    /// имени, и по имени внутри ссылки, и по префиксу ссылки целиком.</summary>
    internal sealed class SourceFilter
    {
        public List<string> Excluded = new List<string>();

        public bool Accepts(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;

            string bare = layerName;
            int bar = layerName.LastIndexOf('|');
            if (bar >= 0) bare = layerName.Substring(bar + 1);

            foreach (string excluded in Excluded)
            {
                if (excluded.EndsWith("|", StringComparison.Ordinal))
                {
                    if (layerName.StartsWith(excluded, StringComparison.CurrentCultureIgnoreCase)) return false;
                    continue;
                }

                if (string.Equals(excluded, layerName, StringComparison.CurrentCultureIgnoreCase)) return false;
                if (string.Equals(excluded, bare, StringComparison.CurrentCultureIgnoreCase)) return false;
            }

            return true;
        }

        public SourceFilter Clone()
        {
            return new SourceFilter { Excluded = new List<string>(Excluded) };
        }
    }
}
