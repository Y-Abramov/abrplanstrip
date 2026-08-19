namespace AbrCivil.PlanStrip.Core.Model
{
    /// <summary>Слой своей графики полосы: имя (без префикса), цвет ACI, тип и вес линии.
    /// Контракт для LayerFactory (Cad, Task 12) - создаётся/находится по этим данным.
    /// LineWeight -3 означает «по умолчанию» (ByLayer-поведение AutoCAD).</summary>
    internal sealed class LayerStyle
    {
        public string LayerName;
        public short ColorIndex = 7;
        public string Linetype = "Continuous";
        public short LineWeight = -3;
    }
}
