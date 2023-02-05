using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
    }

    public class Edge : IIndexed
    {
        public int ID { get; set; } = -1;
        public Index2i Points { get; set; }
        public EdgePositionMode Mode = EdgePositionMode.Undefined;
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
            Mode = other.Mode;
        }

        public int GetNext(int index)
        {
            Debug.Assert(Points.a == index || Points.b == index);
            return (Points.a == index) ? Points.b : Points.a;
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

        public IEnumerable<Tuple<Point, Point>> GetEdges()
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
                yield return new Tuple<Point, Point>(Points[currentPointId], Points[nextPointId]);
                currentPointId = nextPointId;
            }
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

        public static IntersectContour Intersect(IntersectContour left, IntersectContour right)
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
                        Mode = edgePosition.Mode,
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
                var a = edge.Item1;
                var b = edge.Item2;
                if (Geometry2DHelper.IsPointOnLineSegment(point, a.Coord, b.Coord, eps))
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
                var edgeA = edge.Item1.Coord;
                var edgeB = edge.Item2.Coord;
                var cross = Geometry2DHelper.EdgesInterposition(edgeA, edgeB, a, b, eps);
                if (cross.Intersection == IntersectionVariants.NotComputed ||
                    cross.Intersection == IntersectionVariants.InvalidQuery)
                    throw new Exception($"Не посчиталось пересечение между [{edgeA};{edgeB}] и [{a};{b}]");
                if (cross.Intersection == IntersectionVariants.Intersects)
                {
                    if (cross.IntersectionType == EdgeIntersectionType.Point)
                    {
                        if (Geometry2DHelper.EqualPoints(cross.Point0, a, eps) ||
                            Geometry2DHelper.EqualPoints(cross.Point0, b, eps))
                        {
                            cross.IntersectionType = EdgeIntersectionType.ExistingPoint;
                        }
                        if (Geometry2DHelper.EqualPoints(cross.Point0, edgeA, eps) ||
                            Geometry2DHelper.EqualPoints(cross.Point0, edgeB, eps))
                        {

                            cross.IntersectionType = EdgeIntersectionType.ExistingPoint;
                            cross.VtxID = 0;//TODO insert id
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
                    var pointsParentIntersect = edge.Item1.Parents.Intersect(edge.Item2.Parents);
                    Debug.Assert(pointsParentIntersect.Count() == 1);
                    cross.EdgeID = pointsParentIntersect.First();
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
    }
}
