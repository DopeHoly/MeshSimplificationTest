﻿using g3;
using System;
using System.CodeDom;
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
        Segment,
        Unknown
    }

    public class PointPosition : SBRep_Primitive
    {
        public PointPositionMode Mode;
        public int EdgeID;
        public int VtxID;
        public Vector2d Cross;
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

    public class EdgeCrossPosition : SBRep_Primitive
    {
        public IntersectionVariants Intersection;
        public EdgeIntersectionType IntersectionType;

        public Vector2d Point0;
        public Vector2d Point1;
        public int VtxID;
        public int EdgeID;
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
                case EdgeIntersectionType.Segment:
                    return $"Лежит на ребре {EdgeID} в сегменте [{Point0}; {Point1}]";
                case EdgeIntersectionType.Unknown:
                    return $"хуйня какая-то";
                default:
                    break;
            }
            return null;
        }
    }

    public class EdgePosition : SBRep_Primitive
    {
        public EdgePositionMode Mode;
        public IEnumerable<EdgeCrossPosition> Crosses;
        public override string ToString()
        {
            var builder = new StringBuilder();
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
            var facesIds = filteredFaces.Select(face => face.ID).ToList();
            var facedEdgesIds = obj.GetEdgesFromFaces(facesIds);
            var facedVerticesIds = obj.GetVerticesFromEdgesIds(facedEdgesIds);

            var facedEdges = facedEdgesIds.Select(eid => obj.Edges[eid]).ToList();
            var facedVertices = facedVerticesIds.Select(vid => obj.Vertices[vid]).ToList();

            var contourI = ContourI.FromPointList(contour);
            ContourBoolean2DSolver.InitContourPositions(contourI, facedVertices, facedEdges);
            var report = contourI.GetReport();
            //var Loops = filteredFaces.SelectMany(face =>
            //{
            //    var loops = new List<int>();
            //    loops.Add(face.OutsideLoop);
            //    loops.AddRange(face.InsideLoops);
            //    return loops;
            //});
            //var chainCountours = new List<ChainCountour>();
            //foreach (var loopId in Loops)
            //{
            //    var chainCountour = new ChainCountour(contour);
            //    chainCountour.InitFromSBRepLoop(obj, loopId);
            //    chainCountours.Add(chainCountour);
            //}
            //var contr = chainCountours[0];
            //;
            //foreach ( var face in filteredFaces)
            //{
            //    var facesOutsideLoop = obj.GetClosedContourVtx(face.OutsideLoop);

            //    var pointPositions = contour.Select(point => Geometry2DHelper.CalcPointPosition(facesOutsideLoop, point, 1e-6)).ToList();

            //    var edgesCrosses = new List<List<EdgeCrossPosition>>();
            //    for (int i = 0; i < contour.Count() - 1; ++i)
            //    {
            //        var a = contour.ElementAt(i);
            //        var b = contour.ElementAt(i + 1);
            //        edgesCrosses.Add(Geometry2DHelper.CalcEdgeCrossPositions(facesOutsideLoop, a, b, 1e-6).ToList());
            //    }
            //}

            return obj;
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
                    answer.IntersectionType = EdgeIntersectionType.Segment;
                    answer.Point0 = intersector.Point0;
                    answer.Point1 = intersector.Point1;
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

            return answer;
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
            if (sum > eps)
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

    }

    public class ChainCountour
    {
        public List<Vector2d> sourceContour;
        public IndexedCollection<EdgePosition> EdgePositions;
        public IndexedCollection<PointPosition> PointPositions;


        public ChainCountour(IEnumerable<Vector2d> contour) 
        {
            sourceContour = contour.ToList();
            PointPositions = new IndexedCollection<PointPosition>();
            EdgePositions = new IndexedCollection<EdgePosition>();
        }

        public void InitFromSBRepLoop(SBRepObject obj, int lid)
        {
            double eps = 1e-6;
            var loop = obj.GetClosedContourVtx(lid);

            foreach (var point in sourceContour)
            {
                PointPositions.Add(Geometry2DHelper.CalcPointPosition(loop, point, eps));
            }

            for (int i = 0; i < sourceContour.Count(); ++i)
            {
                var edgePosition = new EdgePosition();
                EdgePositions.Add(edgePosition);
                var points = GetPointIdsFromEdgeId(edgePosition.ID);
                var a = sourceContour[points.Item1];
                var b = sourceContour[points.Item2];
                edgePosition.Crosses = Geometry2DHelper.CalcEdgeCrossPositions(loop, a, b, 1e-6);

                //var crossesInPoints = new List<EdgeCrossPosition>();
                //foreach (var cross in edgePosition.Crosses)
                //{
                //    if(cross.IntersectionType == EdgeIntersectionType.Point)
                //    {
                //        if(Geometry2DHelper.EqualPoints(cross.Point0, pointA.
                //    }

                //}
                var pointsCross = GetPointFromEdgeId(edgePosition.ID);
                var pointACross = pointsCross.Item1;
                var pointBCross = pointsCross.Item2;
                if (edgePosition.Crosses.Count() > 0)
                {
                    edgePosition.Mode = EdgePositionMode.Cross;
                }
                else
                {
                    if(pointACross.Mode == PointPositionMode.InPlane || 
                       pointBCross.Mode == PointPositionMode.InPlane)
                    {
                        edgePosition.Mode = EdgePositionMode.InPlane;
                    }
                    else
                        edgePosition.Mode = EdgePositionMode.OutPlane;
                }
            }

        }

        public Tuple<int, int> GetPointIdsFromEdgeId(int eid)
        {
            var pointAid = eid;
            var pointBid = eid + 1;
            if (PointPositions.Count == pointBid)
                pointBid = 0;
            return new Tuple<int, int>(pointAid, pointBid);

        }
        public Tuple<PointPosition, PointPosition> GetPointFromEdgeId(int eid)
        {
            var ids = GetPointIdsFromEdgeId(eid);
            var pointAid = ids.Item1;
            var pointBid = ids.Item2;
            return new Tuple<PointPosition, PointPosition>(PointPositions[pointAid], PointPositions[pointBid]);
        }


    }
}
