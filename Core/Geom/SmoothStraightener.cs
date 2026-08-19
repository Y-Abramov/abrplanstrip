namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Плавная развёртка: каждая точка идёт через пикетаж и смещение.
    /// Прямая в кривой перестаёт быть прямой - для топоосновы в полосе профиля это норма.
    ///
    /// Перенос тотальный: точка вне полосы переносится наравне с остальными.
    /// Границу режет StripRect уже в координатах полосы (ShapeMapper) - иначе линия,
    /// проходящая полосу насквозь, теряется целиком по своим концам.</summary>
    internal sealed class SmoothStraightener
    {
        private readonly ICenterline _line;
        private readonly StripFrame _frame;

        public SmoothStraightener(ICenterline line, StripFrame frame)
        {
            _line = line;
            _frame = frame;
        }

        public StripFrame Frame { get { return _frame; } }

        public bool TryMap(Pt2 world, out Pt2 strip)
        {
            strip = default(Pt2);

            double station, offset;
            if (!_line.TryStationOffset(world, out station, out offset)) return false;

            strip = new Pt2(station - _frame.StartStation, offset * _frame.CrossScale);
            return true;
        }

        public bool TryMapRigid(Pt2 world, double rotation, out Pt2 strip, out double stripRotation)
        {
            strip = default(Pt2);
            stripRotation = 0.0;

            double station, offset;
            if (!_line.TryStationOffset(world, out station, out offset)) return false;

            strip = new Pt2(station - _frame.StartStation, offset * _frame.CrossScale);
            stripRotation = rotation - _line.TangentAt(station);
            return true;
        }
    }
}
