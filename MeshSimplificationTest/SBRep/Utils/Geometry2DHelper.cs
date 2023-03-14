﻿using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest.SBRep.Utils
{
    public static class Geometry2DHelper
    {
        public static bool EqualPoints(Vector2d p1, Vector2d p2, double eps = 1e-6)
        {
            if (!(Math.Abs(p1.x - p2.x) <= eps)) return false;//по X не близко
            if (!(Math.Abs(p1.y - p2.y) <= eps)) return false;//по Y не близко
            return true;
        }
        public static bool EqualVectors(Vector3d p1, Vector3d p2, double eps = 1e-6)
        {
            if (!(Math.Abs(p1.x - p2.x) <= eps)) return false;//по X не близко
            if (!(Math.Abs(p1.y - p2.y) <= eps)) return false;//по Y не близко
            if (!(Math.Abs(p1.z - p2.z) <= eps)) return false;//по Z не близко
            return true;
        }
        public static bool EqualZero(Vector2d p1, double eps = 1e-6)
        {
            return EqualPoints(p1, Vector2d.Zero, eps);
        }

        public static EdgeCrossPosition EdgesInterposition(Vector2d e1v1, Vector2d e1v2, Vector2d e2v1, Vector2d e2v2, double eps)
        {
            var intersector = new IntrSegment2Segment2(new Segment2d(e1v1, e1v2), new Segment2d(e2v1, e2v2));
            intersector.IntervalThreshold = eps;
            intersector.Compute();

            var answer = new EdgeCrossPosition();

            switch (intersector.Result)
            {
                case IntersectionResult.Intersects:
                    answer.Intersection = IntersectionVariants.Intersects;
                    break;
                case IntersectionResult.NoIntersection:
                    answer.Intersection = IntersectionVariants.NoIntersection;
                    break;
                case IntersectionResult.NotComputed:
                    answer.Intersection = IntersectionVariants.NotComputed;
                    break;
                case IntersectionResult.InvalidQuery:
                    answer.Intersection = IntersectionVariants.InvalidQuery;
                    break;
                default: break;
            }
            switch (intersector.Type)
            {
                case IntersectionType.Empty:
                    answer.IntersectionType = EdgeIntersectionType.Empty;
                    break;
                case IntersectionType.Point:
                    answer.IntersectionType = EdgeIntersectionType.Point;
                    answer.Point0 = intersector.Point0;
                    break;
                case IntersectionType.Segment:
                    if(IsPointOnLine(e1v1, e2v1, e2v2, eps) &&
                        IsPointOnLine(e1v2, e2v1, e2v2, eps))
                    {
                        answer.IntersectionType = EdgeIntersectionType.Segment;
                        answer.Point0 = intersector.Point0;
                        answer.Point1 = intersector.Point1;
                    }
                    else
                    {
                        answer.Intersection = IntersectionVariants.NoIntersection;
                        answer.IntersectionType = EdgeIntersectionType.Empty;
                    }
                    break;
                case IntersectionType.Line:
                    answer.IntersectionType = EdgeIntersectionType.Unknown;
                    answer.Point0 = intersector.Point0;
                    answer.Point1 = intersector.Point1;
                    ;
                    break;
                default:
                    break;
            }

            if(answer.IntersectionType == EdgeIntersectionType.Segment)
            {
                if(EqualPoints(answer.Point0, answer.Point1, eps))
                {
                    answer.IntersectionType = EdgeIntersectionType.Point;
                }
            }

            return answer;
        }
        public static int orientation3Points(Vector2d p1, Vector2d p2, Vector2d p3)
        {
            // See 10th slides from following link for derivation
            // of the formula
            int val = (int)((p2.y - p1.y) * (p3.x - p2.x) - (p2.x - p1.x) * (p3.y - p2.y));

            if (val == 0)
                return 0; // collinear

            return (val > 0) ? 1 : 2; // clock or counterclock wise
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
            for (int i = 0; i < contour.Count(); ++i)
            {
                var bindex = i + 1;
                if (bindex == contour.Count())
                    bindex = 0;

                var a = contour.ElementAt(i);
                var b = contour.ElementAt(bindex);
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
            }

            PointPositionMode mode = PointPositionMode.OutPlane;
            if (InContour(contour.Select(x => x.Coordinate.xy), point))
            {
                mode = PointPositionMode.InPlane;
            }

            return new PointPosition()
            {
                Mode = mode
            };
        }

        /// <summary>
        /// https://stackoverflow.com/questions/4243042/c-sharp-point-in-polygon
        /// </summary>
        /// <param name="contour"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool InContour(IEnumerable<Vector2d> contour, Vector2d point, double eps = 1e-6)
        {
            var polygon = contour.ToList();
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++)
            {
                if (polygon[i].y < point.y && polygon[j].y >= point.y || polygon[j].y < point.y && polygon[i].y >= point.y)
                {
                    if (polygon[i].x + (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < point.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
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
                var segmentLenght = (l2- l1).Length;
                var pL1Lenght = (l1- pt).Length;
                var pL2Lenght = (l2- pt).Length;
                var sum = pL1Lenght + pL2Lenght;
                var dif = Math.Abs(segmentLenght - sum);
                return dif < eps;
            }
            return false;
        }

        public static bool IsPointOnLine(Vector2d pt, Vector2d l1, Vector2d l2, double eps = 0.0)
        {
            return Math.Abs((pt.x - l1.x) * (l2.y - l1.y) - (pt.y - l1.y) * (l2.x - l1.x)) <= eps;
        }


        public static IEnumerable<EdgeCrossPosition> CalcEdgeCrossPositions(IEnumerable<SBRep_Vtx> contour, Vector2d v1, Vector2d v2, double eps)
        {
            var crosses = new List<EdgeCrossPosition>();

            for (int i = 0; i < contour.Count() - 1; ++i)
            {
                var a = contour.ElementAt(i).Coordinate;
                var b = contour.ElementAt(i + 1).Coordinate;
                var cross = Geometry2DHelper.EdgesInterposition(a.xy, b.xy, v1, v2, eps);
                if (cross.Intersection == IntersectionVariants.NotComputed ||
                    cross.Intersection == IntersectionVariants.InvalidQuery)
                    throw new Exception($"Не посчиталось пересечение между [{a.xy};{b.xy}] и [{v1};{v2}]");
                if (cross.Intersection == IntersectionVariants.Intersects)
                {
                    crosses.Add(cross);
                }
            }

            return crosses;
        }

        private static double CalcT(Vector2d a, Vector2d b, KeyValuePair<int, Vector2d> point, double eps = 1e-12)
        {
            var p = point.Value;
            var leftOperand = (Math.Abs(b.x - a.x) > eps);
            var rightOperand = (Math.Abs(b.y - a.y) > eps);
            double sum = 0.0;
            if (leftOperand)
                sum = (point.Value.x - a.x) / (b.x - a.x);
            if (rightOperand && !leftOperand)
                sum = (point.Value.y - a.y) / (b.y - a.y);
            return sum;
        }

        public static Dictionary<int, Vector2d> SortPointsOnEdge(Vector2d a, Vector2d b, Dictionary<int, Vector2d> points)
        {
            if (Geometry2DHelper.EqualPoints(a, b))
            {
                //throw new Exception("Точки a и b совпадают. Сортировка невозможна");
                return points;
            }
            var tDict = new Dictionary<int, double>();
            foreach (var point in points)
            {
                var t = CalcT(a, b, point);
                tDict.Add(point.Key,t);
            }
            var sortedDict = from entry in tDict orderby entry.Value ascending select entry;

            var result = new Dictionary<int, Vector2d>();
            foreach (var t in sortedDict)
            {
                var key = t.Key;
                result.Add(key, points[key]);
            }
            return result;
        }

        /// <summary>
        /// Вычисляет площадь внутри двухмерного контура points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double GetArea(List<Vector2d> points)
        {
            return Math.Abs(GetAreaSigned(points));
        }

        /// <summary>
        /// Вычисляет площадь внутри двухмерного контура points с учётом знака
        /// </summary>
        /// <param name="points"></param>
        /// <returns>если больше нуля, то точки расположены против часовой</returns>
        public static double GetAreaSigned(List<Vector2d> points)
        {
            var points2D = new List<Vector2d>();
            var first = points.First();
            points2D.Add(points.Last());
            points2D.AddRange(points);
            points2D.Add(first);

            var cnt = points2D.Count - 2;
            decimal sum = 0.0M;
            for (int i = 1; i <= cnt; ++i)
            {
                sum += (decimal)points2D[i].x * ((decimal)points2D[i + 1].y - (decimal)points2D[i - 1].y);
            }
            var area = (double)sum / 2.0;
            return area;
        }

        public static Tuple<Matrix3d, Matrix3d, Vector3d> CalculateTransform(
            Vector3d pointOnContour,
            Vector3d normal)
        {
            var pointOnVector = pointOnContour;

            normal = normal.Normalized;
            Vector3d vectorM = Vector3d.Zero;
            if (Math.Abs(normal.x) > Math.Max(normal.y, normal.z))
            {
                vectorM = new Vector3d(normal.y, -normal.x, 0.0);
            }
            else
            {
                vectorM = new Vector3d(0, normal.z, -normal.y);
            }

            vectorM = vectorM.Normalized;
            var vectorP = vectorM.Cross(normal);

            var mtx = new Matrix3d(vectorP, vectorM, normal, true);
            var inverseMtx = new Matrix3d(vectorP, vectorM, normal, false);
            return new Tuple<Matrix3d, Matrix3d, Vector3d>(mtx, inverseMtx, pointOnVector);
        }
    }
}
