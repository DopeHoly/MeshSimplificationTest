using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace MeshSimplificationTest.SBRep
{
    public class PointI
    {
        public int SourceID = -1;
        public Vector2d Coord { get; set; }
        public PointPosition Position { get; set; } = null;
        public PointI() 
        {
            Coord = Vector2d.Zero;
        }
        public PointI(Vector2d coord)
        {
            this.Coord = coord;
        }
        public PointI(SBRep_Vtx vtx)
        {
            Coord = vtx.Coordinate.xy;
            SourceID = vtx.ID;
        }
    }

    public class EdgeI
    {
        public int SourceID = -1;
        public Index2i Points;
        public EdgePosition Position { get; set; } = null;
        public EdgeI(int a = -1, int b = -1)
        {
            Points = new Index2i(a, b);
        }
        public EdgeI(SBRep_Edge edge)
        {
            Points = edge.Vertices;
            SourceID = edge.ID;
        }
    }

    public class ContourI
    {
        public List<PointI> Vertices { get; protected set; }
        public List<EdgeI> Edges { get; protected set; }

        public ContourI()
        {
            Vertices = new List<PointI>();
            Edges = new List<EdgeI>();
        }

        public int GetEdgeID(EdgeI edge)
        {
            return Edges.IndexOf(edge);
        }

        public int GetPointID(PointI point)
        {
            return Vertices.IndexOf(point);
        }

        public Tuple<int, int> GetEdgesPointsIds(int eid)
        {
            var pointAid = eid;
            var pointBid = eid + 1;
            if (Vertices.Count == pointBid)
                pointBid = 0;
            return new Tuple<int, int>(pointAid, pointBid);
        }
        public Tuple<PointI, PointI> GetPointFromEdgeId(int eid)
        {
            var ids = GetEdgesPointsIds(eid);
            var pointAid = ids.Item1;
            var pointBid = ids.Item2;
            return new Tuple<PointI, PointI>(
                Vertices[pointAid], 
                Vertices[pointBid]);
        }

        public Tuple<int, int> GetEdgesIdsFromPoint(int pid)
        {
            var EdgeAid = pid - 1;
            var EdgeBid = pid;
            if (EdgeAid < 0 )
                EdgeBid = Edges.Count() - 1;
            return new Tuple<int, int>(EdgeAid, EdgeBid);
        }
        public Tuple<EdgeI, EdgeI> GetEdgesFromPoint(int pid)
        {
            var ids = GetEdgesIdsFromPoint(pid);
            var EdgeAid = ids.Item1;
            var EdgeBid = ids.Item2;
            return new Tuple<EdgeI, EdgeI>(
                Edges[EdgeAid],
                Edges[EdgeBid]);
        }

        public static ContourI FromPointList(IEnumerable<Vector2d> vertices)
        {
            var contour = new ContourI();
            foreach ( var v in vertices)
            {
                contour.Vertices.Add(new PointI(v));
            }

            for(int i = 0; i < vertices.Count() - 1; ++i)
            {
                contour.Edges.Add(new EdgeI(i, i + 1));
            }
            contour.Edges.Add(new EdgeI(vertices.Count() - 1, 0));
            return contour;
        }

        public string GetReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("По точкам:");
            foreach (var point in Vertices)
            {
                builder.AppendLine($"{point.Coord}: {point.Position.ToString()}");
            }
            builder.AppendLine("По граням:");
            foreach (var edge in Edges)
            {
                builder.AppendLine($"{edge.Points}: {edge.Position.ToString()}");
            }

            return builder.ToString();
        }
    }

    public class ContourBoolean
    {
        public ContourBoolean() 
        {
        }
    }

    public static class ContourBoolean2DSolver
    {
        public static void InitContourPositions(
            ContourI contour, 
            IEnumerable<SBRep_Vtx> vertices,
            IEnumerable<SBRep_Edge> edges, 
            double eps = 1e-6)
        {
            foreach (var point in contour.Vertices)
            {
                point.Position = Geometry2DHelper.CalcPointPosition(
                    vertices, 
                    point.Coord, 
                    eps);
            }

            foreach (var edge in contour.Edges)
            {
                var eid = contour.GetEdgeID(edge);
                var edgesPoints = contour.GetPointFromEdgeId(eid);
                var pA = edgesPoints.Item1;
                var pB = edgesPoints.Item2;
                edge.Position = CalcEdgePositions(
                    pA.Coord,
                    pB.Coord,
                    vertices, edges, eps);

                if (edge.Position.Crosses.Count() > 0)
                {
                    edge.Position.Mode = EdgePositionMode.Cross;
                }
                else
                {
                    if (pA.Position.Mode == PointPositionMode.InPlane ||
                       pB.Position.Mode == PointPositionMode.InPlane)
                    {
                        edge.Position.Mode = EdgePositionMode.InPlane;
                    }
                    else
                        edge.Position.Mode = EdgePositionMode.OutPlane;
                }
            }
        } 

        public static EdgePosition CalcEdgePositions(
            Vector2d a, Vector2d b,
            IEnumerable<SBRep_Vtx> vertices,
            IEnumerable<SBRep_Edge> edges,
            double eps)
        {
            var crosses = new List<EdgeCrossPosition>();
            foreach (var edge in edges)
            {
                var edgeA = vertices.First(vtx => vtx.ID.Equals(edge.Vertices.a)).Coordinate.xy;
                var edgeB = vertices.First(vtx => vtx.ID.Equals(edge.Vertices.b)).Coordinate.xy;
                var cross = Geometry2DHelper.EdgesInterposition(edgeA, edgeB, a, b, eps);
                if (cross.Intersection == IntersectionVariants.NotComputed ||
                    cross.Intersection == IntersectionVariants.InvalidQuery)
                    throw new Exception($"Не посчиталось пересечение между [{edgeA};{edgeB}] и [{a};{b}]");
                if (cross.Intersection == IntersectionVariants.Intersects)
                {
                    if(cross.IntersectionType == EdgeIntersectionType.Point)
                    {
                        if (Geometry2DHelper.EqualPoints(cross.Point0, a, eps) ||
                            Geometry2DHelper.EqualPoints(cross.Point0, b, eps))
                            continue;
                        if (Geometry2DHelper.EqualPoints(cross.Point0, edgeA, eps) ||
                            Geometry2DHelper.EqualPoints(cross.Point0, edgeB, eps))
                            continue;
                    }
                    cross.EdgeID = edge.ID;
                    crosses.Add(cross);
                }
            }

            var position = new EdgePosition()
            {
                Crosses = crosses,
                Mode = EdgePositionMode.Undefined
            };

            return position;
        }

    }
}
