using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MeshSimplificationTest.SBRep
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

    public class EdgeCrossPosition
    {
        public EdgeCrossPositionMode Mode;
        public Vector2d CrossPoint;
        public int VtxID;
        public int EdgeID;
    }

    public class EdgePosition
    {
        public EdgePositionMode Mode;
        public IEnumerable<EdgeCrossPosition> Crosses;
    }


    public static class SBRepOperations
    {
        public static SBRepObject ContourProjection(this SBRepObject obj, List<Vector2d> contour, bool topDirection)
        {
            //copy object
            //var sbrep = new SBRepObject();

            //получаем boundingbox контура

            //строим словари близости точек и граней

            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            var filteredFaces = obj.Faces.Where(face => comparer(face.Normal.z)).ToList();

            foreach ( var face in filteredFaces)
            {
                var facesOutsideLoop = obj.GetClosedContourVtx(face.OutsideLoop);

                var pointPositions = contour.Select(point => CalcPointPosition(facesOutsideLoop, point, 1e-6)).ToList();
            }

            return obj;
        }

        public static PointPosition CalcPointPosition(IEnumerable<SBRep_Vtx> contour, Vector2d point, double eps)
        {
            foreach (var vertex in contour)
            {
                if (Geometry2DHelper.EqualPoints(vertex.Coordinate.xy, point, eps))
                    return new PointPosition()
                    {
                        Mode = PointPositionMode.OnVertex,
                        VtxID = vertex.ID
                    };
            }

            double sum = 0;
            for (int i = 0; i < contour.Count() - 1; ++i)
            {
                var a = contour.ElementAt(i);
                var b = contour.ElementAt(i + 1);
                if (IsPointOnLineSegment(point, a.Coordinate.xy, b.Coordinate.xy, eps))
                {
                    var pointsParentIntersect = a.Parents.Intersect(b.Parents);
                    Debug.Assert(pointsParentIntersect.Count() == 1);
                    var edgeID = pointsParentIntersect.First();
                    return new PointPosition()
                    {
                        Mode = PointPositionMode.OnEdge,
                        EdgeID = edgeID
                    };
                }
                sum += Vector2d.AngleD(
                    a.Coordinate.xy - point,
                    b.Coordinate.xy - point);

            }
            PointPositionMode mode = PointPositionMode.OutPlane;
            if(sum > eps)
            {
                mode = PointPositionMode.InPlane;
            }

            return new PointPosition() 
            {
                Mode = mode 
            };
        }

        public static bool PointInContour(List<Vector2d> contour, Vector2d point, double eps)
        {

            double sum = 0;
            for (int i = 0; i < contour.Count - 1; i++)
            {
                if (IsPointOnLineSegment(point, contour[i], contour[i + 1], 1e-6))
                    return false;
                sum += Vector2d.AngleD(
                    contour[i] - point,
                    contour[i + 1] - point);
            }
            return Math.Abs(sum) > eps;
        }

        /// <summary>
        /// Проверка находится ли точка на отрезке
        /// </summary>
        /// <param name="pt">Точка</param>
        /// <param name="l1">Точка отрезка 1</param>
        /// <param name="l2">Точка отрезка 2</param>
        /// <returns></returns>
        public static bool IsPointOnLineSegment(Vector2d pt, Vector2d l1, Vector2d l2, double eps)
        {
            if (IsPointOnLine(pt, l1, l2, eps))
            {
                double minx = Math.Min(l1.x, l2.x);
                double maxx = Math.Max(l1.x, l2.x);
                double dx = maxx - minx;
                if (dx == 0)
                {
                    double miny = Math.Min(l1.y, l2.y);
                    double maxy = Math.Max(l1.y, l2.y);
                    return (pt.y <= maxy && pt.y >= miny);
                }
                else
                {
                    return (pt.x <= maxx && pt.x >= minx);
                }
            }
            return false;
        }

        public static bool IsPointOnLine(Vector2d pt, Vector2d l1, Vector2d l2, double eps = 0.0)
        {
            return Math.Abs((pt.x - l1.x) * (l2.y - l1.y) - (pt.y - l1.y) * (l2.x - l1.x)) <= eps;
        }


        public static IEnumerable<EdgeCrossPosition> CalcEdgeCrossPositions(IEnumerable<SBRep_Vtx> contour, Vector2d v1, Vector2d v2, double eps)
        {

        }
    }

    public static class Geometry2DHelper
    {
        public static bool EqualPoints(Vector2d p1, Vector2d p2, double eps = 0.0)
        {
            if (!(Math.Abs(p1.x - p2.x) <= eps)) return false;//по X не близко
            if (!(Math.Abs(p1.y - p2.y) <= eps)) return false;//по Y не близко
            return true;
        }

        public static void EdgesInterposition(Vector2d e1v1, Vector2d e1v2, Vector2d e2v1, Vector2d e2v2, double eps)
        {
            var e1Isv2e1 = EqualPoints(e1v1, e2v1); 
        }
        
    }
}
