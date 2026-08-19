using System;
using System.Collections.Generic;

namespace AbrCivil.PlanStrip.Core.Geom
{
    /// <summary>Развёртка «по углам»: трасса режется на куски по вершинам углов поворота,
    /// каждый кусок переносится жёстким преобразованием - внутри куска ничего не деформируется.
    /// На стыках возникают клинья и нахлёсты: их пикеты отдаём наверх, там рисуются границы стыков.</summary>
    internal sealed class CornerStraightener
    {
        private readonly ICenterline _line;
        private readonly StripFrame _frame;
        private readonly List<double> _seams;

        public CornerStraightener(ICenterline line, StripFrame frame)
        {
            _line = line;
            _frame = frame;
            _seams = BuildSeams(line, frame);
        }

        public StripFrame Frame { get { return _frame; } }

        public IList<double> SeamStations()
        {
            return _seams;
        }

        public bool TryMap(Pt2 world, out Pt2 strip)
        {
            double rotation;
            return TryMapRigid(world, 0.0, out strip, out rotation);
        }

        public bool TryMapRigid(Pt2 world, double rotation, out Pt2 strip, out double stripRotation)
        {
            strip = default(Pt2);
            stripRotation = 0.0;

            double station, offset;
            if (!_line.TryStationOffset(world, out station, out offset)) return false;

            // Границы полосы здесь не проверяются: режет StripRect уже в координатах
            // полосы (ShapeMapper). Отбор по вершинам убивал линии, проходящие насквозь.
            double pieceStart = PieceStart(station);
            double azimuth = _line.TangentAt(pieceStart);

            Pt2 origin = _line.PointAt(pieceStart, 0.0);

            // Поворот куска на -азимут вокруг точки оси в начале куска
            double dx = world.X - origin.X, dy = world.Y - origin.Y;
            double cos = Math.Cos(-azimuth), sin = Math.Sin(-azimuth);

            double rx = dx * cos - dy * sin;
            double ry = dx * sin + dy * cos;

            strip = new Pt2(
                pieceStart - _frame.StartStation + rx,
                ry * _frame.CrossScale);

            stripRotation = rotation - azimuth;
            return true;
        }

        private double PieceStart(double station)
        {
            double start = _frame.StartStation;
            foreach (double seam in _seams)
            {
                if (seam <= station + 1e-9) start = seam;
                else break;
            }
            return start;
        }

        private static List<double> BuildSeams(ICenterline line, StripFrame frame)
        {
            var seams = new List<double>();
            foreach (var corner in line.Corners)
            {
                if (corner.Station <= frame.StartStation) continue;
                if (corner.Station >= frame.EndStation) continue;
                seams.Add(corner.Station);
            }
            seams.Sort();
            return seams;
        }
    }
}
