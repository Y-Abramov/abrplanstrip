namespace AbrCivil.PlanStrip.Core.Report
{
    internal sealed class NullProgress : IProgressSink
    {
        public static readonly NullProgress Instance = new NullProgress();

        private NullProgress()
        {
        }

        public void Begin(string phaseCaption, int total)
        {
        }

        public void Tick()
        {
        }

        public void End()
        {
        }
    }
}
