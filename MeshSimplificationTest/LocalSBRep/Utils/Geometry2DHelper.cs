using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SBRep.Utils
{
    public static class Geometry2DHelper
    {
        public static bool Between(double x, double minX, double maxX, double eps)
        {
            return (((x >= (minX - eps)) && (x <= (maxX + eps))) ||
                ((x >= (maxX - eps)) && (x <= (minX + eps))));
        }

        public static bool EqualPoints(Vector2d p1, Vector2d p2, double eps = 1e-6)
        {
            return (p1 - p2).LengthSquared <= eps * eps;
        }

        public static bool EqualVectors(Vector3d p1, Vector3d p2, double eps = 1e-6)
        {
            return (p1 - p2).LengthSquared <= eps * eps;
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
                    if (IsPointOnLine(e1v1, e2v1, e2v2, eps) &&
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

            if (answer.IntersectionType == EdgeIntersectionType.Segment)
            {
                if (EqualPoints(answer.Point0, answer.Point1, eps))
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
                if (EqualPoints(vertex.Coordinate.xy, point, eps))
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
            if (InContour(contour.Select(x => x.Coordinate.xy), point, eps))
            {
                mode = PointPositionMode.InPlane;
            }

            return new PointPosition()
            {
                Mode = mode
            };
        }

        /// <summary>
        /// Determines if the given point is inside the polygon
        /// https://stackoverflow.com/questions/4243042/c-sharp-point-in-polygon
        /// </summary>
        /// <param name="contour">the vertices of polygon</param>
        /// <param name="point">the given point</param>
        /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool InContour(IEnumerable<Vector2d> contour, Vector2d point, double eps = 1e-6)//TODO подозрительно 
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
                if (IsPointOnLineSegment(point, contour[i], contour[i + 1], eps))
                    return false;
                sum += Vector2d.AngleD(
                    contour[i] - point,
                    contour[i + 1] - point);
            }
            return Math.Abs(sum) > eps;
        }

        public static Vector2d RotateVector(Vector2d pt, double sin, double cos, bool cwDirrect = true, double offsetX = 0, double offsetY = 0)
        {
            var sign = cwDirrect? 1 : -1;
            return new Vector2d(
                pt.x * cos + sign * pt.y * sin + offsetX,
                -1 * sign * pt.x * sin + pt.y * cos + offsetY
                );
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
            if (EqualPoints(pt, l1, eps))
                return true;
            if (EqualPoints(pt, l2, eps))
                return true;

            if (eps == 0) eps = 1e-12;

            var lineVector = l2 - l1;
            var axisXVector = new Vector2d(1, 0);

            var cosAngle = lineVector.Normalized.Dot(axisXVector);
            cosAngle = Math.Round(cosAngle, 12, MidpointRounding.AwayFromZero);
            var sinAngle = Math.Sqrt(1 - cosAngle * cosAngle);
            sinAngle = Math.Round(sinAngle, 12, MidpointRounding.AwayFromZero);

            var ptR = RotateVector(pt - l1, sinAngle, cosAngle, lineVector.y >= 0);
            var maxX = lineVector.Length;
            
            var x = ptR.x;
            var y = ptR.y;
            return x >= 0 && x <= maxX && Math.Abs(y) <= eps;
        }

        public static bool PointInRectangle(Vector2d m,
            Vector2d a,
            Vector2d b,
            Vector2d c,
            Vector2d d)
        {
            var AB = a - b;
            var AM = a - m;
            var BC = b - c;
            var BM = b - m;
            var dotABAM = AB.Dot(AM);
            var dotABAB = AB.Dot(AB);
            var dotBCBM = BC.Dot(BM);
            var dotBCBC = BC.Dot(BC);
            return 0 <= dotABAM && dotABAM <= dotABAB && 0 <= dotBCBM && dotBCBM <= dotBCBC;
        }

        public static double PointToLineDistance(Vector2d pt, Vector2d l1, Vector2d l2)
        {
            double a = l2.y - l1.y;
            double b = -(l2.x - l1.x);
            double c = l1.y*l2.x - l2.y * l1.x;
            var devider = Math.Sqrt(a * a + b * b);
            if(devider == 0)
            {
                return (pt - l1).Length;
            }
            var distance = Math.Abs(a * pt.x + b * pt.y + c) / devider;
            return distance;
        }

        public static bool IsPointOnLine(Vector2d pt, Vector2d l1, Vector2d l2, double eps = 0.0)
        {
            return PointToLineDistance(pt, l1, l2) <= eps;
        }

        public static Vector2d PerpendicularClockwise(this Vector2d vector2)
        {
            return new Vector2d(-vector2.x, vector2.y);
        }

        public static Vector2d PerpendicularCounterClockwise(this Vector2d vector2)
        {
            return new Vector2d(vector2.x, -vector2.y);
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
                tDict.Add(point.Key, t);
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
            double sum = 0.0;
            for (int i = 1; i <= cnt; ++i)
            {
                sum += points2D[i].x * (points2D[i + 1].y - points2D[i - 1].y);
            }
            var area = sum / 2.0;
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
        
        /// <summary>
        /// Вычисляет двухмерные координаты трёхмерного контура
        /// </summary>
        /// <param name="contour">плоский контур с 3д координатами</param>
        /// <param name="normal">нормаль поверхности, на которой находится контур</param>
        /// <returns></returns>
        public static List<Vector2d> ConvertTo2D(IEnumerable<Vector3d> contour, Vector3d normal)
        {
            var transformMtxs = Geometry2DHelper.CalculateTransform(contour.First(), normal);
            var offset = transformMtxs.Item3;
            var mtx = transformMtxs.Item1;
            var points2d = new List<Vector2d>();
            foreach (var vertice in contour)
            {
                //смещение в 0
                var point = new Vector3d(vertice.x - offset.x, vertice.y - offset.y, vertice.z - offset.z);
                //применение матрицы преобразования в плоскость XOY
                var point2D = mtx * point;
                points2d.Add(new Vector2d(point2D.x, point2D.y));
            }
            return points2d;
        }

        public static Vector3d GetWeightedCenter(IEnumerable<Vector3d> contour)
        {
            var cnt = contour.Count();
            var sum = 0.0;
            var dict = new Dictionary<int, double>();
            for (int i = 0; i < cnt; i++)
            {
                var nextIndex = i + 1;
                if (cnt == nextIndex)
                    nextIndex = 0;
                var len = (contour.ElementAt(i) - contour.ElementAt(nextIndex)).Length;
                dict.Add(i, len);
                sum += len;
            }

            var center = new Vector3d(0, 0, 0);
            for (int i = 0; i < cnt; i++)
            {
                var nextIndex = i + 1;
                if (cnt == nextIndex)
                    nextIndex = 0;

                var centerEdge = (contour.ElementAt(i) + contour.ElementAt(nextIndex)) / 2.0;
                var weight = dict[i] / sum;
                center += centerEdge * weight;
            }
            return center;
        }
    }
}
