using AbrCivil.PlanStrip.Core.Report;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Добавляет префикс к подписи каждой фазы - используется в
    /// PLANSTRIPUPDATE при обновлении всех полос разом, чтобы на статусбаре
    /// было видно, какая именно полоса сейчас строится.</summary>
    internal sealed class ScopedProgress : IProgressSink
    {
        private readonly IProgressSink _inner;
        private readonly string _prefix;

        public ScopedProgress(IProgressSink inner, string prefix)
        {
            _inner = inner;
            _prefix = prefix;
        }

        public void Begin(string phaseCaption, int total)
        {
            _inner.Begin(_prefix + phaseCaption, total);
        }

        public void Tick()
        {
            _inner.Tick();
        }

        public void End()
        {
            _inner.End();
        }
    }
}
