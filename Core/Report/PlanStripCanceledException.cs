using System;

namespace AbrCivil.PlanStrip.Core.Report
{
    /// <summary>Пользователь прервал построение полосы клавишей ESC.</summary>
    internal sealed class PlanStripCanceledException : Exception
    {
        public PlanStripCanceledException()
            : base("Построение полосы отменено пользователем.")
        {
        }
    }
}
