using g3;
using MeshSimplificationTest.SBRep.SBRepOperations;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        //public Vector2d Cross;
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

    public class EdgeCrossPosition : IEquatable<EdgeCrossPosition>
    {
        public IntersectionVariants Intersection;
        public EdgeIntersectionType IntersectionType;

        public Vector2d Point0;
        public Vector2d Point1;
        public int VtxID;
        public int EdgeID;

        public bool Equals(EdgeCrossPosition other)
        {
            if(Intersection == other.Intersection)
            {
                if(Intersection == IntersectionVariants.Intersects)
                {
                    if (IntersectionType == other.IntersectionType)
                    {
                        if (IntersectionType == EdgeIntersectionType.Point ||
                            IntersectionType == EdgeIntersectionType.ExistingPoint)
                        {
                            return Geometry2DHelper.EqualPoints(Point0, other.Point0);
                        }
                        if(IntersectionType == EdgeIntersectionType.Segment ||
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


    public static class SBRepOperationsExtension
    {
        public static SBRepObject ContourProjection(this SBRepObject sbrep, List<Vector2d> contour, bool topDirection)
        {

            var obj = new SBRepObject(sbrep);
            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            var filteredFaces = obj.Faces.Where(face => comparer(face.Normal.z)).ToList();

            //Берём все петли, которые встречаются в выбранных гранях
            var loopsIDs = filteredFaces.SelectMany(face =>
            {
                var loops = new List<int>();
                loops.Add(face.OutsideLoop);
                loops.AddRange(face.InsideLoops);
                return loops;
            }).Distinct().ToList();

            var projectionContour = IntersectContour.FromPoints(contour);

            var loopsIntersectContours = new Dictionary<int, IntersectContour>();
            foreach (var lid in loopsIDs)
            {
                var loopVertices = obj.GetClosedContourVtx(lid);
                var loopEdges = obj.GetClosedContourEdges(lid);
                var loopContour = IntersectContour.FromSBRepLoop(loopVertices, loopEdges);

                loopsIntersectContours.Add(lid, loopContour);
            }

            foreach (var face in filteredFaces)
            {
                var projContour = new IntersectContour(projectionContour);
                var outsideLoop = new IntersectContour(loopsIntersectContours[face.OutsideLoop]);
                var insideLoops = face.InsideLoops.Select(lid =>
                 new IntersectContour(loopsIntersectContours[lid]))
                    .ToList();
                var resultIntersect = IntersectContour.Intersect(outsideLoop, projContour);
                foreach (var insideLoop in insideLoops)
                {
                    resultIntersect = IntersectContour.Difference(projContour, outsideLoop);
                }
                ApplyIntersectContourToFace(obj, face.ID, resultIntersect);
            }

            return obj;
        }

        public static void ApplyIntersectContourToFace(SBRepObject sbrep, int faceID, IntersectContour contour)
        {
            //TODO очень важно!!!!!

            //сначала добавляем все точки из contour в sbrep
            var pointsInPlane = contour.Points.Where(point => point.Position.Mode == PointPositionMode.InPlane);
            var pointsExisting = contour.Points.Where(point => point.Position.Mode == PointPositionMode.OnVertex);
            var pointsOnEdge = contour.Points.Where(point => point.Position.Mode == PointPositionMode.OnEdge);

            // потом заново берём петлю

            //добавляем новые рёбра


            //вычисляем, какие рёбра внутри, какие снаружи

            //вычисляем множества рёбер для новых и старых граней

            //Собираем петли для новых граней

            //Собираем петли для старых граней

            //пересобираем части петель

            //пересобираем грани

            //применяем изменения к sbrep
        }

        public static void Experements(SBRepObject obj, List<Vector2d> contour, bool topDirection)
        {
            //copy object
            var sbrep = new SBRepObject();

            //получаем boundingbox контура

            //строим словари близости точек и граней

            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            var filteredFaces = obj.Faces.Where(face => comparer(face.Normal.z)).ToList();
            //var facesIds = filteredFaces.Select(face => face.ID).ToList();
            //var facedEdgesIds = obj.GetEdgesFromFaces(facesIds);
            //var facedVerticesIds = obj.GetVerticesFromEdgesIds(facedEdgesIds);

            //var facedEdges = facedEdgesIds.Select(eid => obj.Edges[eid]).ToList();
            //var facedVertices = facedVerticesIds.Select(vid => obj.Vertices[vid]).ToList();

            //var contourI = ContourI.FromPointList(contour);
            //ContourBoolean2DSolver.InitContourPositions(contourI, facedVertices, facedEdges);
            //var report = contourI.GetReport();
            var Loops = filteredFaces.SelectMany(face =>
            {
                var loops = new List<int>();
                loops.Add(face.OutsideLoop);
                loops.AddRange(face.InsideLoops);
                return loops;
            }).Distinct().ToList();

            var loopContourIntersect = new Dictionary<int, ContourI>();
            foreach (var lid in Loops)
            {
                var facesOutsideLoop = obj.GetClosedContourVtx(lid);
                var contourIntersect = ContourI.FromPointList(contour);
                var faceEdges = obj.GetClosedContourEdges(lid);

                ContourBoolean2DSolver.InitContourPositions(contourIntersect, facesOutsideLoop, faceEdges);
                loopContourIntersect.Add(lid, contourIntersect);

                var faceID = obj.Loops[lid].Parents.First();
                var plane = obj.Faces[faceID].Plane;
                var report = contourIntersect.GetReport();
                var coord = contourIntersect.GetPointsInCross();
                var coord3d = coord.Select(point => new Vector3d(point.x, point.y, plane.GetZ(point.x, point.y)));
                foreach (var item in coord3d)
                {
                    sbrep.Vertices.Add(new SBRep_Vtx()
                    {
                        Coordinate = item
                    });
                }
                //for(int i = 0; i < coord3d.Count(); i++)
                //{
                //    var nextIndex = i + 1;
                //    if (nextIndex == coord3d.Count())
                //        nextIndex = 0;
                //    sbrep.Edges.Add(new SBRep_Edge()
                //    {
                //        Vertices = new Index2i(i, nextIndex)
                //    });
                //}
            }
            //var loopContourIntersectPoints = loopContourIntersect.First().Value.GetPointsInCross();

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

            //return sbrep;
        }
    }


    public static class Geometry2DHelper
    {
        public static bool EqualPoints(Vector2d p1, Vector2d p2, double eps = 1e-6)
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

    //public class ChainCountour
    //{
    //    public List<Vector2d> sourceContour;
    //    public IndexedCollection<EdgePosition> EdgePositions;
    //    public IndexedCollection<PointPosition> PointPositions;


    //    public ChainCountour(IEnumerable<Vector2d> contour) 
    //    {
    //        sourceContour = contour.ToList();
    //        PointPositions = new IndexedCollection<PointPosition>();
    //        EdgePositions = new IndexedCollection<EdgePosition>();
    //    }

    //    public void InitFromSBRepLoop(SBRepObject obj, int lid)
    //    {
    //        double eps = 1e-6;
    //        var loop = obj.GetClosedContourVtx(lid);

    //        foreach (var point in sourceContour)
    //        {
    //            PointPositions.Add(Geometry2DHelper.CalcPointPosition(loop, point, eps));
    //        }

    //        for (int i = 0; i < sourceContour.Count(); ++i)
    //        {
    //            var edgePosition = new EdgePosition();
    //            EdgePositions.Add(edgePosition);
    //            var points = GetPointIdsFromEdgeId(edgePosition.ID);
    //            var a = sourceContour[points.Item1];
    //            var b = sourceContour[points.Item2];
    //            edgePosition.Crosses = Geometry2DHelper.CalcEdgeCrossPositions(loop, a, b, 1e-6);

    //            //var crossesInPoints = new List<EdgeCrossPosition>();
    //            //foreach (var cross in edgePosition.Crosses)
    //            //{
    //            //    if(cross.IntersectionType == EdgeIntersectionType.Point)
    //            //    {
    //            //        if(Geometry2DHelper.EqualPoints(cross.Point0, pointA.
    //            //    }

    //            //}
    //            var pointsCross = GetPointFromEdgeId(edgePosition.ID);
    //            var pointACross = pointsCross.Item1;
    //            var pointBCross = pointsCross.Item2;
    //            if (edgePosition.Crosses.Count() > 0)
    //            {
    //                edgePosition.Mode = EdgePositionMode.Cross;
    //            }
    //            else
    //            {
    //                if(pointACross.Mode == PointPositionMode.InPlane || 
    //                   pointBCross.Mode == PointPositionMode.InPlane)
    //                {
    //                    edgePosition.Mode = EdgePositionMode.InPlane;
    //                }
    //                else
    //                    edgePosition.Mode = EdgePositionMode.OutPlane;
    //            }
    //        }

    //    }

    //    public Tuple<int, int> GetPointIdsFromEdgeId(int eid)
    //    {
    //        var pointAid = eid;
    //        var pointBid = eid + 1;
    //        if (PointPositions.Count == pointBid)
    //            pointBid = 0;
    //        return new Tuple<int, int>(pointAid, pointBid);

    //    }
    //    public Tuple<PointPosition, PointPosition> GetPointFromEdgeId(int eid)
    //    {
    //        var ids = GetPointIdsFromEdgeId(eid);
    //        var pointAid = ids.Item1;
    //        var pointBid = ids.Item2;
    //        return new Tuple<PointPosition, PointPosition>(PointPositions[pointAid], PointPositions[pointBid]);
    //    }


    //}
}
