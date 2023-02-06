using g3;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using static g3.DPolyLine2f;

namespace MeshSimplificationTest.SBRep.SBRepOperations
{
    public class Point : IIndexed
    {
        public int ID { get; set; } = -1;
        public ICollection<int> Parents { get; set; }

        public PointPosition Position { get; set; }

        public Vector2d Coord { get; set; }

        public Point(Vector2d coord) 
        { 
            Coord = coord;
            Parents = new List<int>();
        }

        public Point(Point other)
        {
            Copy(other);
        }
        public void Copy(Point other)
        {
            this.ID = other.ID;
            this.Coord = other.Coord;
            this.Parents = new List<int>(other.Parents);
            this.Position = other.Position;
        }

        public override string ToString()
        {
            string parents = "(";
            foreach (var item in Parents)
            {
                parents += item.ToString() + " ";
            }
            parents += ")";
            return $"PointID{ID} {Coord}, {parents}: {Position}";
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

    public class Edge : IIndexed
    {
        public int ID { get; set; } = -1;
        public Index2i Points { get; set; }
        public ShortEdgePosition Position;
        public Edge(Index2i points) 
        {
            Points = points;
        }
        public Edge(int a, int b)
        {
            Points = new Index2i(a, b);
        }

        public Edge(Edge other)
        {
            Copy(other);
        }
        public void Copy(Edge other)
        {
            ID = other.ID;
            Points = other.Points;
            Position = other.Position;
        }

        public int GetNext(int index)
        {
            Debug.Assert(Points.a == index || Points.b == index);
            return (Points.a == index) ? Points.b : Points.a;
        }

        public override string ToString()
        {
            return $"EdgeID{ID} {Points}: {Position}";
        }
    }

    public class Vector2dEqualityComparer : IEqualityComparer<Vector2d>
    {
        public bool Equals(Vector2d x, Vector2d y)
        {
            return Geometry2DHelper.EqualPoints(x, y);
        }

        public int GetHashCode(Vector2d obj)
        {
            const int digits = 6;
            int hCode = Math.Round(obj.x, digits).GetHashCode() ^ Math.Round(obj.y, digits).GetHashCode();
            return hCode;
        }
    }

    public class IntersectContour
    {
        public IndexedCollection<Point> Points { get; set; }
        public IndexedCollection<Edge> Edges { get; set;}

        public int Count => Points.Count;

        private IntersectContour() 
        {
            Points = new IndexedCollection<Point>();
            Edges = new IndexedCollection<Edge>();
        }

        public IntersectContour(IntersectContour other) : this()
        {
            Copy(other);
        }

        public void Copy(IntersectContour other)
        {
            foreach (var point in other.Points)
            {
                Points.Add(new Point(point));
            }
            foreach (var edge in other.Edges)
            {
                Edges.Add(new Edge(edge));
            }
        }

        public IEnumerable<Point> GetContour()
        {
            var currentPointId = -1;
            var startEdge = Edges.First();
            var startEdgeId = startEdge.ID;
            var startPointId = startEdge.Points.a;
            var currentEdge = startEdge;
            var currentEdgeId = -1;
            while (startPointId != currentPointId)
            {
                if (currentPointId == -1)
                    currentPointId = startPointId;
                var currentPoint = Points[currentPointId];
                yield return currentPoint;

                var nextEdgeID = currentPoint.Parents.First(eid => eid != currentEdgeId);
                currentEdge = Edges[nextEdgeID];
                currentEdgeId = nextEdgeID;
                currentPointId = currentEdge.GetNext(currentPointId);
            }
        }

        public IEnumerable<Edge> GetEdges()
        {
            var currentPointId = -1;
            var startEdge = Edges.First();
            var startEdgeId = startEdge.ID;
            var startPointId = startEdge.Points.a;
            var currentEdge = startEdge;
            var currentEdgeId = -1;
            while (startPointId != currentPointId)
            {
                if (currentPointId == -1)
                    currentPointId = startEdge.Points.a;
                var currentPoint = Points[currentPointId];

                var nextEdgeID = currentPoint.Parents.First(eid => eid != currentEdgeId);
                currentEdge = Edges[nextEdgeID];

                currentEdgeId = nextEdgeID;
                var nextPointId = currentEdge.GetNext(currentPointId);
                yield return currentEdge;
                currentPointId = nextPointId;
            }
        }

        public Tuple<Point, Point> GetEdgePoints(Edge edge)
        {
            var a = edge.Points.a;
            var b = edge.Points.b;
            return new Tuple<Point, Point>(Points[a], Points[b]);
        }

        public void ReindexPointsParents()
        {
            foreach (var point in Points)
            {
                point.Parents.Clear();
            }
            foreach(var edge in Edges)
            {
                Points[edge.Points.a].Parents.Add(edge.ID);
                Points[edge.Points.b].Parents.Add(edge.ID);
            }
        }


        public static IntersectContour FromPoints(IEnumerable<Vector2d> vertices)
        {
            var contour = new IntersectContour();
            if (vertices == null || vertices.Count() == 0)
                return contour;
            foreach (var v in vertices)
            {
                contour.Points.Add(new Point(v));
            }

            for (int i = 0; i < vertices.Count() - 1; ++i)
            {
                contour.Edges.Add(new Edge(i, i + 1));
            }
            contour.Edges.Add(new Edge(vertices.Count() - 1, 0));
            contour.ReindexPointsParents();
            return contour;
        }

        public static IntersectContour FromSBRepLoop(
            IEnumerable<SBRep_Vtx> vertices,
            IEnumerable<SBRep_Edge> edges)
        {
            var result = new IntersectContour();
            if (vertices == null || vertices.Count() == 0 ||
                edges == null || edges.Count() == 0)
                return result;
            foreach (var item in vertices)
            {
                result.Points.Add(new Point(item.Coordinate.xy)
                {
                    ID = item.ID
                });
            }
            foreach (var item in edges)
            {
                result.Edges.Add(new Edge(item.Vertices)
                {
                    ID = item.ID
                });
            }
            result.ReindexPointsParents();
            return result;
        }

        public static IntersectContour IntersectOld(IntersectContour left, IntersectContour right)
        {
            var eps = 1e-6;
            if(left == null || right == null) return null;
            var intersect = new IntersectContour();

            Debug.Assert(left.Edges.Count == left.Points.Count);
            Debug.Assert(right.Edges.Count == right.Points.Count);

            var count = left.Count;
            var startPointId = left.Points.First().ID;
            var currentPointId = -1;
            Edge currentEdge = null;
            var currentEdgeId = -1;
            while (startPointId != currentPointId)
            {
                if(currentPointId == -1)
                    currentPointId = startPointId;

                var currentPoint = left.Points[currentPointId];

                //Тут определяем, как точка пересекается с right контуром
                var pointPosition = CalcPointPosition(right, currentPoint.Coord, eps);

                var newPoint = new Point(currentPoint.Coord)
                {
                    Position = pointPosition,
                };
                intersect.Points.Add(newPoint);
                var lastCreatedPointId = newPoint.ID;
                var nextCreatingPointId = intersect.Points.Count;
                //Тут определем, как ребро пересекается с right контуром и добавляем по этим результатам новые точки и рёбра в результаты
                //Берём следующее ребро
                var nextEdgeID = currentPoint.Parents.First(eid => eid != currentEdgeId);
                currentEdge = left.Edges[nextEdgeID];
                currentEdgeId = nextEdgeID;
                var nextPointId = currentEdge.GetNext(currentPointId);

                var edgePosition = CalcEdgePositions(left.Points[currentPointId].Coord, left.Points[nextPointId].Coord, right, eps);
                
                if (edgePosition.Mode == EdgePositionMode.InPlane ||
                    edgePosition.Mode == EdgePositionMode.OutPlane)
                {
                    intersect.Edges.Add(new Edge(lastCreatedPointId, nextCreatingPointId)//TODO пересмотреть
                    {
                        Position = new ShortEdgePosition()
                        {
                            Mode = ShortEdgePositionMode.Undefined
                        },
                    });
                }
                else
                {
                    foreach (var cross in edgePosition.Crosses)
                    {
                        Debug.Assert(cross.Intersection == IntersectionVariants.Intersects);
                        if (cross.IntersectionType == EdgeIntersectionType.Point)
                        {
                            var a = lastCreatedPointId;
                            var crossPoint = new Point(cross.Point0)
                            {
                                Position = new PointPosition()
                                {
                                    EdgeID = cross.EdgeID,
                                }
                            };
                            intersect.Points.Add(crossPoint);
                            nextCreatingPointId = intersect.Points.Count;
                            intersect.Edges.Add(new Edge(lastCreatedPointId, crossPoint.ID));
                            intersect.Edges.Add(new Edge(crossPoint.ID, nextCreatingPointId));
                            lastCreatedPointId = crossPoint.ID;

                        }
                        else if (cross.IntersectionType == EdgeIntersectionType.ExistingPoint)
                        {
                            var a = lastCreatedPointId;
                            var crossPoint = new Point(cross.Point0)
                            {
                                Position = new PointPosition()
                                {
                                    VtxID = cross.VtxID,
                                }
                            };
                            intersect.Points.Add(crossPoint);
                            nextCreatingPointId = intersect.Points.Count;
                            intersect.Edges.Add(new Edge(lastCreatedPointId, crossPoint.ID));
                            intersect.Edges.Add(new Edge(crossPoint.ID, nextCreatingPointId));
                            lastCreatedPointId = crossPoint.ID;

                        }
                        else if (cross.IntersectionType == EdgeIntersectionType.AllEdge)
                        {

                        }
                        else if (cross.IntersectionType == EdgeIntersectionType.Segment)
                        {

                        }
                        else
                            throw new Exception("Не предусмотренно");
                    }
                }

                currentPointId = nextPointId;


            }
            intersect.ReindexPointsParents();
            return intersect;
        }

        public static IntersectContour Intersect(IntersectContour left, IntersectContour right)
        {
            var eps = 1e-6;
            if (left == null || right == null) return null;
            var intersect = new IntersectContour();

            Debug.Assert(left.Edges.Count == left.Points.Count);
            Debug.Assert(right.Edges.Count == right.Points.Count);

            var pointsIndexesDictionary = new Dictionary<int, int>();
            foreach (var point in left.GetContour())
            {
                var position = CalcPointPosition(right, point.Coord, eps);

                var newPoint = new Point(point.Coord)
                {
                    Position = position,
                };
                intersect.Points.Add(newPoint);
                var originalIndex = point.ID;
                var newIndex = newPoint.ID;
                pointsIndexesDictionary.Add(originalIndex, newIndex);
            }
            foreach (var edge in left.GetEdges())
            {
                var edgesPoints = left.GetEdgePoints(edge);
                var edgeA = edgesPoints.Item1.Coord;
                var edgeB = edgesPoints.Item2.Coord;
                var edgePosition = CalcEdgePositions(edgeA, edgeB, right, eps);
                var edges = SeparateEdgeFromEdgePosition(
                    intersect, 
                    right,
                    edgePosition,
                    pointsIndexesDictionary[edge.Points.a],
                    pointsIndexesDictionary[edge.Points.b]);
                foreach (var eid in edges)
                {
                    var currentEdge = intersect.Edges[eid];
                    edgesPoints = intersect.GetEdgePoints(currentEdge);
                    var pointA = edgesPoints.Item1;
                    var pointB = edgesPoints.Item2;
                    //тут мы подразумеваем, что edge без пересечений
                    if (pointA.Position.Mode == PointPositionMode.InPlane ||
                        pointB.Position.Mode == PointPositionMode.InPlane)
                    {
                        currentEdge.Position.Mode = ShortEdgePositionMode.InPlane;
                    }
                    else
                    if (pointA.Position.Mode == PointPositionMode.OutPlane ||
                        pointB.Position.Mode == PointPositionMode.OutPlane)
                    {
                        currentEdge.Position.Mode = ShortEdgePositionMode.OutPlane;
                    }
                    else
                    if (pointA.Position.Mode == PointPositionMode.OnVertex &&
                        pointB.Position.Mode == PointPositionMode.OnVertex) //TODO сломается на случае пересечения в двух точках вне контура
                    {
                        currentEdge.Position.Mode = ShortEdgePositionMode.ExistingEdge;
                        var aOrigId = pointA.Position.VtxID;
                        var bOrigId = pointB.Position.VtxID;
                        var indexAB = new Index2i(aOrigId, bOrigId);
                        var indexBA = new Index2i(bOrigId, aOrigId);
                        currentEdge.Position.EdgeId = right.Edges.First(e => e.Points == indexAB || e.Points == indexBA).ID;
                    }
                    else
                    if (pointA.Position.Mode == PointPositionMode.OnEdge ||
                        pointB.Position.Mode == PointPositionMode.OnEdge) //TODO пересмотреть
                    {
                        currentEdge.Position.Mode = ShortEdgePositionMode.EdgeSegment;
                        var pointOnEdge = pointA.Position.Mode == PointPositionMode.OnEdge ? pointA : pointB;
                        currentEdge.Position.EdgeId = pointOnEdge.Position.EdgeID;
                    }
                    else
                    {
                        throw new Exception();//TODO
                    }
                }
            }

            Debug.Assert(intersect.Edges.Count == intersect.Points.Count);
            Debug.Assert(!intersect.Edges.Any(edge => edge.Position.Mode == ShortEdgePositionMode.Undefined));
            intersect.ReindexPointsParents();

            return intersect;
        }

        public static IntersectContour Difference(IntersectContour left, IntersectContour right)
        {
            return null;
        }

        public static PointPosition CalcPointPosition(IntersectContour contour, Vector2d point, double eps)
        {
            foreach (var vertex in contour.Points)
            {
                if (Geometry2DHelper.EqualPoints(vertex.Coord, point, eps))
                    return new PointPosition()
                    {
                        Mode = PointPositionMode.OnVertex,
                        VtxID = vertex.ID
                    };
            }

            foreach (var edge in contour.GetEdges())
            {
                var edgesPoints = contour.GetEdgePoints(edge);
                var a = edgesPoints.Item1;
                var b = edgesPoints.Item2;
                if (Geometry2DHelper.IsPointOnLineSegment(point, a.Coord, b.Coord, eps))
                {
                    //var pointsParentIntersect = a.Parents.Intersect(b.Parents);
                    //Debug.Assert(pointsParentIntersect.Count() == 1);
                    //var edgeID = pointsParentIntersect.First();
                    return new PointPosition()
                    {
                        Mode = PointPositionMode.OnEdge,
                        EdgeID = edge.ID
                    };
                }
            }

            PointPositionMode mode = PointPositionMode.OutPlane;
            if (Geometry2DHelper.InContour(contour.GetContour().Select(x => x.Coord), point))
            {
                mode = PointPositionMode.InPlane;
            }

            return new PointPosition()
            {
                Mode = mode
            };
        }

        public static EdgePosition CalcEdgePositions(
            Vector2d a, Vector2d b,
            IntersectContour contour,
            double eps)
        {
            var crosses = new List<EdgeCrossPosition>();
            foreach (var edge in contour.GetEdges())
            {
                var edgesPoints = contour.GetEdgePoints(edge);
                var edgeA = edgesPoints.Item1.Coord;
                var edgeB = edgesPoints.Item2.Coord;
                var cross = Geometry2DHelper.EdgesInterposition(edgeA, edgeB, a, b, eps);
                if (cross.Intersection == IntersectionVariants.NotComputed ||
                    cross.Intersection == IntersectionVariants.InvalidQuery)
                    throw new Exception($"Не посчиталось пересечение между [{edgeA};{edgeB}] и [{a};{b}]");
                if (cross.Intersection == IntersectionVariants.Intersects)
                {
                    if (cross.IntersectionType == EdgeIntersectionType.Point)
                    {
                        //такие пересечения нам не интересны, точки а и б и без этого добавятся
                        if (Geometry2DHelper.EqualPoints(cross.Point0, a, eps) ||
                            Geometry2DHelper.EqualPoints(cross.Point0, b, eps))
                        {
                            //cross.IntersectionType = EdgeIntersectionType.ExistingPoint;
                            continue;
                        }
                        if (Geometry2DHelper.EqualPoints(cross.Point0, edgeA, eps))
                        {
                            cross.IntersectionType = EdgeIntersectionType.ExistingPoint;
                            cross.VtxID = edgesPoints.Item1.ID;
                        }
                        if(Geometry2DHelper.EqualPoints(cross.Point0, edgeB, eps))
                        {
                            cross.IntersectionType = EdgeIntersectionType.ExistingPoint;
                            cross.VtxID = edgesPoints.Item2.ID;
                        }
                    }
                    if (cross.IntersectionType == EdgeIntersectionType.Segment)
                    {
                        var aEqual = Geometry2DHelper.EqualPoints(cross.Point0, edgeA, eps);
                        var bEqual = Geometry2DHelper.EqualPoints(cross.Point1, edgeB, eps);
                        if (aEqual && bEqual)
                        {
                            cross.IntersectionType = EdgeIntersectionType.AllEdge;
                        }
                        else
                        {
                            aEqual = Geometry2DHelper.EqualPoints(cross.Point1, edgeA, eps);
                            bEqual = Geometry2DHelper.EqualPoints(cross.Point0, edgeB, eps);
                            if (aEqual && bEqual)
                            {
                                cross.IntersectionType = EdgeIntersectionType.AllEdge;
                            }
                        }
                    }
                    if(cross.IntersectionType != EdgeIntersectionType.ExistingPoint)
                    {
                        //var pointsParentIntersect = edge.Item1.Parents.Intersect(edge.Item2.Parents);
                        //Debug.Assert(pointsParentIntersect.Count() == 1);
                        cross.EdgeID = edge.ID;
                    }
                    crosses.Add(cross);
                }
            }

            //тут нужно почистить от дубликатов и отсортировать (при надобности)
            if(crosses.Count > 0)
            {
                crosses = crosses.Distinct().ToList();
            }

            var mode = EdgePositionMode.Undefined;
            if (crosses.Count > 0)
                mode = EdgePositionMode.Cross;

            var position = new EdgePosition()
            {
                Crosses = crosses,
                Mode = mode
            };

            return position;
        }

        public static IEnumerable<int> SeparateEdgeFromEdgePosition(
            IntersectContour target,
            IntersectContour right,
            EdgePosition edgePosition,
            int indexA,
            int indexB,
            double eps = 1e-6)
        {
            if (edgePosition.Mode != EdgePositionMode.Cross)
            {
                var newEdge = new Edge(indexA, indexB)
                {
                    Position = new ShortEdgePosition()
                    {
                        Mode = ShortEdgePositionMode.Undefined
                    },
                };
                target.Edges.Add(newEdge);
                return new List<int> { newEdge.ID};
            }
            var newEdges = new List<int>();
            var pointComparer = new Vector2dEqualityComparer();
            var newPoints = new List<Vector2d>();
            foreach (var cross in edgePosition.Crosses)
            {
                if (cross.IntersectionType == EdgeIntersectionType.Segment ||
                    cross.IntersectionType == EdgeIntersectionType.AllEdge)
                {
                    newPoints.Add(cross.Point0);
                    newPoints.Add(cross.Point1);
                }
                if (cross.IntersectionType == EdgeIntersectionType.ExistingPoint ||
                    cross.IntersectionType == EdgeIntersectionType.Point)
                {
                    newPoints.Add(cross.Point0);
                }
            }
            var pointA = target.Points[indexA].Coord;
            var pointB = target.Points[indexB].Coord;

            //оставляем только уникальные точки и убираем A и B
            var uniqueue = newPoints.Distinct(pointComparer).ToList();
            var uniquePoints = uniqueue.Where(
                point => !pointComparer.Equals(point, pointA) && !pointComparer.Equals(point, pointB)).ToList();
            //добавляем точки в контур
            var uniquePointsDict = new Dictionary<int, Vector2d>();
            var counter = 0;
            foreach (var item in uniquePoints)
            {
                uniquePointsDict.Add(counter, item);
                ++counter;
            }
            var pointsIndexesDictionary = new Dictionary<int, int>();
            foreach (var point in uniquePointsDict)
            {
                var position = CalcPointPosition(right, point.Value, eps);

                var newPoint = new Point(point.Value)
                {
                    Position = position,
                };
                target.Points.Add(newPoint);
                var originalIndex = point.Key;
                var newIndex = newPoint.ID;
                pointsIndexesDictionary.Add(originalIndex, newIndex);
            }
            //сортируем точки и выделяем сегменты
            var sortedPoints = Geometry2DHelper.SortPointsOnEdge(pointA, pointB, uniquePointsDict);
            var previewsPointId = indexA;
            for(int i = 0; i < sortedPoints.Count + 1; ++i)
            {
                int currentPointId = -1;
                if (i == sortedPoints.Count)
                    currentPointId = indexB;
                else
                    currentPointId = pointsIndexesDictionary[sortedPoints.ElementAt(i).Key];
                var newEdge = new Edge(previewsPointId, currentPointId)
                {
                    Position = new ShortEdgePosition()
                    {
                        Mode = ShortEdgePositionMode.Undefined
                    },
                };
                target.Edges.Add(newEdge);
                newEdges.Add(newEdge.ID);
                previewsPointId = currentPointId;
            }
            //опрделяем, как располагается ребро относительно контура

            return newEdges;
        }


        public override string ToString()
        {
            var builder = new StringBuilder();
            if (Points.Count > 0)
            {
                builder.AppendLine("По точкам:");
                foreach (var point in Points)
                {
                    builder.AppendLine(point.ToString());
                }
            }
                
            if(Edges.Count > 0)
            {
                builder.AppendLine("По граням:");
                foreach (var edge in Edges)
                {
                    builder.AppendLine(edge.ToString());
                }
            }

            return builder.ToString();
        }
    }
}
