namespace AbrCivil.PlanStrip.Core.Model
{
    /// <summary>Полоса, как она хранится в чертеже. Геометрия не пишется:
    /// она восстанавливается пересчётом по трассе и текущему состоянию источников.</summary>
    internal sealed class StripModel
    {
        public string Id;
        public string AlignmentName;
        public string ProfileViewHandle;
        public string BlockName;
        public double StartStation;
        public double EndStation;
        public StripSettings Settings = new StripSettings();
    }
}
