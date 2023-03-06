using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest.SBRep.Utils
{
    public class Graph_Point : IIndexed
    {
        public int ID { get; set; } = -1;
        public ICollection<int> Parents { get; set; }
        public Graph_Point()
        {
            Parents = new List<int>();
        }
        public override string ToString()
        {
            string parents = "(";
            foreach (var item in Parents)
            {
                parents += item.ToString() + " ";
            }
            parents += ")";
            return $"Graph_Point {ID}: {parents}";
        }
    }

    public class Graph_Edge : IIndexed
    {
        public int ID { get; set; } = -1;
        public Index2i Points { get; set; }
    }

    public class Graph
    {
        public IndexedCollection<Graph_Point> Points { get; set; }
        public IndexedCollection<Graph_Edge> Edges { get; set; }

        private Graph()
        {
            Points = new IndexedCollection<Graph_Point>();
            Edges = new IndexedCollection<Graph_Edge>();
        }

        public static Graph FromSBRepEdges(SBRepObject obj, IEnumerable<int> edgesIds)
        {
            Graph graph = new Graph();

            var edges = edgesIds.Select(eid => obj.Edges[eid]).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.Vertices.a, edge.Vertices.b }
                )
                .Distinct()
                .ToList();

            var vertices = verticesIds.Select(vid => obj.Vertices[vid]).ToList();

            foreach (var edge in edges)
            {
                graph.Edges.Add(new Graph_Edge()
                {
                    ID = edge.ID,
                    Points = edge.Vertices
                });
            }

            foreach (var points in vertices)
            {
                graph.Points.Add(new Graph_Point()
                {
                    ID = points.ID
                });
            }
            graph.ReindexPointsParents();

            return graph;
        }

        public void ReindexPointsParents()
        {
            foreach (var point in Points)
            {
                point.Parents.Clear();
            }
            foreach (var edge in Edges)
            {
                if (!Points.ContainsKey(edge.Points.a))
                    throw new Exception();
                if (!Points.ContainsKey(edge.Points.b))
                    throw new Exception();
                Points[edge.Points.a].Parents.Add(edge.ID);
                Points[edge.Points.b].Parents.Add(edge.ID);
            }
        }

        public IEnumerable<int> GetLoop()
        {
            int startEdge = Edges.First().ID;
            int currentEdge = -1;
            var lastVtxID = -1;
            var vertexOrderList = new List<int>();
            while (startEdge != currentEdge)
            {
                if (currentEdge == -1)
                    currentEdge = startEdge;

                var edge = Edges[currentEdge];
                //edgesIDs.RemoveAt(currentEdge);
                var verticeIndexes = edge.Points;
                int vtxID = -1;
                if (lastVtxID == -1)
                {
                    vtxID = verticeIndexes.a;
                }
                else
                {
                    vtxID = verticeIndexes.a == lastVtxID ? verticeIndexes.b : verticeIndexes.a;
                }
                var vtx = Points[vtxID];
                vertexOrderList.Add(vtx.ID);
                lastVtxID = vtx.ID;

                var nextEdge = vtx.Parents.Where(eid => eid != currentEdge).ToList();

                if (nextEdge.Count == 0)
                    throw new Exception("Нет пути");
                if (nextEdge.Count != 1)
                    throw new Exception("У петли почему то появилась развилка");
                currentEdge = nextEdge.First();
            }

            return vertexOrderList;
        }
    }
}
