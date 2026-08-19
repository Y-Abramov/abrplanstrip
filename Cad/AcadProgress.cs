using AbrCivil.PlanStrip.Core.Report;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace AbrCivil.PlanStrip.Cad
{
    /// <summary>Статусбар AutoCAD как приёмник прогресса. Один экземпляр переживает
    /// несколько фаз и несколько полос подряд (PLANSTRIPUPDATE «Enter»),
    /// ProgressMeter поддерживает повторные Start/Stop.
    ///
    /// ESC во время Tick бросает PlanStripCanceledException - вызывающий код
    /// (Extension.cs) откатывает транзакцию, ничего частично построенного
    /// в чертеже не остаётся.</summary>
    internal sealed class AcadProgress : IProgressSink
    {
        private readonly ProgressMeter _meter = new ProgressMeter();

        public void Begin(string phaseCaption, int total)
        {
            _meter.SetLimit(total > 0 ? total : 1);
            _meter.Start(phaseCaption);
        }

        public void Tick()
        {
            if (HostApplicationServices.Current.UserBreak())
                throw new PlanStripCanceledException();

            _meter.MeterProgress();
        }

        public void End()
        {
            _meter.Stop();
        }
    }
}
