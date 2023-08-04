using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBRep.Utils
{
    public enum PointPositionMode
    {
        Undefined,
        InPlane,
        OutPlane,
        OnEdge,
        OnVertex
    }

    public enum EdgePositionMode
    {
        Undefined,
        InPlane,
        OutPlane,
        Cross
    }

    public enum EdgeCrossPositionMode
    {
        Undefined, //не посчитан
        None, //не пересекает
        EdgeIntrsection, //пересекается грань контура
        PointIntersection, //пересекается в вершине контура
        EdgeConstaind, //полностью лежит на грани
        EdgeSeparete //частично лежит на грани
    }
    public enum IntersectionVariants
    {
        NotComputed,
        Intersects,
        NoIntersection,
        InvalidQuery
    }

    public enum EdgeIntersectionType
    {
        Empty,
        Point,
        ExistingPoint,
        Segment,
        AllEdge,
        Unknown
    }

    public class PointPosition
    {
        public PointPositionMode Mode;
        public int EdgeID;
        public int VtxID;
        public PointPosition()
        {
            Mode = PointPositionMode.Undefined;
            EdgeID = -1;
            VtxID = -1;
        }

        public PointPosition(PointPosition other)
        {
            Mode = other.Mode;
            EdgeID = other.EdgeID;
            VtxID = other.VtxID;
        }

        public override string ToString()
        {
            switch (Mode)
            {
                case PointPositionMode.Undefined:
                    return "Не расчитанно";
                case PointPositionMode.InPlane:
                    return "Внутри контура";
                case PointPositionMode.OutPlane:
                    return "Вне контура";
                case PointPositionMode.OnEdge:
                    return $"На ребре {EdgeID}";
                case PointPositionMode.OnVertex:
                    return $"В точке {VtxID}";
                default:
                    break;
            }
            return null;
        }
    }

    public class EdgeCrossPosition : IEquatable<EdgeCrossPosition>
    {
        public IntersectionVariants Intersection;
        public EdgeIntersectionType IntersectionType;

        public Vector2d Point0;
        public Vector2d Point1;
        public int VtxID = -1;
        public int EdgeID = -1;

        public bool Equals(EdgeCrossPosition other)
        {
            if (Intersection == other.Intersection)
            {
                if (Intersection == IntersectionVariants.Intersects)
                {
                    if (IntersectionType == other.IntersectionType)
                    {
                        if (IntersectionType == EdgeIntersectionType.Point ||
                            IntersectionType == EdgeIntersectionType.ExistingPoint)
                        {
                            return Geometry2DHelper.EqualPoints(Point0, other.Point0);
                        }
                        if (IntersectionType == EdgeIntersectionType.Segment ||
                            IntersectionType == EdgeIntersectionType.AllEdge)
                        {
                            return Geometry2DHelper.EqualPoints(Point0, other.Point0) &&
                                Geometry2DHelper.EqualPoints(Point1, other.Point1);
                        }
                    }
                    else
                        return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var itersectHash = Intersection.GetHashCode();
            var itersectTypeHash = IntersectionType.GetHashCode();
            var vtxHash = VtxID.GetHashCode();
            var edgeHash = EdgeID.GetHashCode();
            return itersectHash ^ itersectTypeHash ^ vtxHash ^ edgeHash;
        }

        public override bool Equals(object obj)
        {
            var ecp = obj as EdgeCrossPosition;
            if (ecp == null)
                return false;
            return Equals(ecp);
        }

        public override string ToString()
        {
            switch (Intersection)
            {
                case IntersectionVariants.NotComputed:
                    return "Не расчитанно";
                case IntersectionVariants.NoIntersection:
                    return "Не пересекает";
                case IntersectionVariants.InvalidQuery:
                    return "Некорректный запрос";
                default:
                    break;
            }
            switch (IntersectionType)
            {
                case EdgeIntersectionType.Point:
                    return $"Пересекает ребро {EdgeID} в точке {Point0}";
                case EdgeIntersectionType.ExistingPoint:
                    return $"Пересекает ребро {EdgeID} в существующей точке {VtxID}";
                case EdgeIntersectionType.Segment:
                    return $"Лежит на ребре {EdgeID} в сегменте [{Point0}; {Point1}]";
                case EdgeIntersectionType.AllEdge:
                    return $"Соответствует ребру {EdgeID} в сегменте [{Point0}; {Point1}]";
                case EdgeIntersectionType.Unknown:
                    return $"хуйня какая-то";
                default:
                    break;
            }
            return null;
        }
    }

    public class EdgePosition
    {
        public int SourceID;
        public EdgePositionMode Mode;
        public IEnumerable<EdgeCrossPosition> Crosses;

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"SourceId: {SourceID}; ");
            switch (Mode)
            {
                case EdgePositionMode.Undefined:
                    builder.Append("Не расчитанно");
                    break;
                case EdgePositionMode.InPlane:
                    builder.Append("Внутри контура");
                    break;
                case EdgePositionMode.OutPlane:
                    builder.Append("Снаружи контура");
                    break;
                case EdgePositionMode.Cross:
                    builder.AppendLine("Имеет Пересечения:");
                    builder.AppendLine("[");
                    foreach (var cross in Crosses)
                    {
                        builder.AppendLine(cross.ToString());
                    }
                    builder.Append("]");
                    break;
                default:
                    break;
            }
            return builder.ToString();
        }
    }

    public enum ShortEdgePositionMode
    {
        Undefined,
        InPlane,
        OutPlane,
        ExistingEdge,
        EdgeSegment
    }

    public class ShortEdgePosition
    {
        public ShortEdgePositionMode Mode;
        public int EdgeId = -1;
        public ShortEdgePosition() { }
        public ShortEdgePosition(ShortEdgePosition other)
        {
            Mode = other.Mode;
            EdgeId = other.EdgeId;
        }
        public override string ToString()
        {
            switch (Mode)
            {
                case ShortEdgePositionMode.Undefined:
                    return "Не расчитанно";
                case ShortEdgePositionMode.InPlane:
                    return "Внутри контура";
                case ShortEdgePositionMode.OutPlane:
                    return "Снаружи контура";
                case ShortEdgePositionMode.ExistingEdge:
                    return $"Совпадает с ребром {EdgeId}";
                case ShortEdgePositionMode.EdgeSegment:
                    return $"Лежит на ребре {EdgeId}";
                default:
                    break;
            }
            return null;
        }
    }
}
