namespace AbrCivil.PlanStrip.Core.Report
{
    /// <summary>Приёмник прогресса построения полосы. Core от AutoCAD не зависит -
    /// реализация (статусбар) живёт в Cad-слое, юнит-тесты используют NullProgress.</summary>
    internal interface IProgressSink
    {
        void Begin(string phaseCaption, int total);
        void Tick();
        void End();
    }
}
