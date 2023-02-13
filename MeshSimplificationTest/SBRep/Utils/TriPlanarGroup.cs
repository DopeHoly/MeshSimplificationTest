using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest.SBRep.Utils
{
    /// <summary>
    /// Представляет собой группу треугольников из mesh
    /// Все треугольники должны лежать в одной плоскости
    /// </summary>
    public class TriPlanarGroup : IIndexed
    {
        public int ID { get; set; } = -1;
        public DMesh3 mesh;
        public Vector3d Normal;
        public int GroupId { get; set; } = -1;
        public List<int> TriangleIDs = new List<int>();

        public TriPlanarGroup(DMesh3 mesh)
        {
            this.mesh = mesh;
        }

        public PlaneFace GetPlane()
        {
            var fTID = TriangleIDs.First();
            Vector3d v0 = Vector3d.Zero;
            Vector3d v1 = Vector3d.Zero;
            Vector3d v2 = Vector3d.Zero;
            mesh.GetTriVertices(fTID, ref v0, ref v1, ref v2);
            var plane = PlaneFace.FromPoints(v0, v1, v2, Normal);
            return plane;
        }

        /// <summary>
        /// Вычисляет список граничных отрезков данной плоскости
        /// </summary>
        /// <returns>Список индексов edge из mesh</returns>
        public List<int> GetBoundaryEdges()
        {
            var bondaryEdgeIDs = new List<int>();
            foreach (var tid in TriangleIDs)
            {
                var edges = mesh.GetTriEdges(tid);

                for (int i = 0; i <= 2; ++i)
                {
                    var currentEdgeID = edges[i];
                    var tries = mesh.GetEdgeT(currentEdgeID);
                    var neigborTri = tries.a == tid ? tries.b : tries.a;

                    if (!TriangleIDs.Contains(neigborTri))
                    {
                        bondaryEdgeIDs.Add(currentEdgeID);
                    }
                }
            }

            //var result = new List<List<int>>();
            //result.Add(bondaryEdgeIDs);
            return bondaryEdgeIDs;
        }

        /// <summary>
        /// Проверяет, все ли точки данной плоскости лежат на одной плоскости
        /// с погрешностью EPS_PointOnPlane = 1e-3
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            if (TriangleIDs == null || TriangleIDs.Count < 1) return false;
            var plane = GetPlane();
            Vector3d v0 = Vector3d.Zero;
            Vector3d v1 = Vector3d.Zero;
            Vector3d v2 = Vector3d.Zero;
            foreach (var tid in TriangleIDs)
            {
                mesh.GetTriVertices(tid, ref v0, ref v1, ref v2);
                var result =
                    plane.PointOnPlane(v0) &&
                    plane.PointOnPlane(v1) &&
                    plane.PointOnPlane(v2);
                if (!result) return false;
            }

            return true;
        }

        /// <summary>
        /// Возвращает петли
        /// </summary>
        /// <returns>MeshRegionBoundaryLoops - класс из g3 либы, я не совсем уверен, что он в 100 прцентах случаев делает то, что нужно</returns>
        public IEnumerable<IEnumerable<int>> GetLoops()
        {
            MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(mesh, TriangleIDs.ToArray());
#if DEBUG
            var edges = GetBoundaryEdges();
            foreach (var loop in loops)
            {
                if (!loop.Edges.All(x => edges.Contains(x)))
                    throw new Exception();
            }
#endif
            var result = new List<IEnumerable<int>>();
            foreach (var loop in loops)
            {
                result.Add(loop.Edges);
            }
            return result;
        }

        private int GetVertexNeigborIdByEdge(int vid, int eid)
        {
            var edgePoints = mesh.GetEdgeV(eid);
            if (edgePoints.a == vid || edgePoints.b == vid)
                return edgePoints.a == vid ? edgePoints.b : edgePoints.a;
            throw new Exception($"Ребро {eid} не содержит {vid}");
        }
        public IEnumerable<IEnumerable<int>> GetLoops2()
        {
            var edgesIDs = GetBoundaryEdges();
            //проверяем критерий обходимости
            var edges = edgesIDs.Select(eid => mesh.GetEdgeV(eid)).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.a, edge.b }
                )
                .Distinct()
                .ToList();
            var vertParentsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var vid in verticesIds)
            {
                var parents = mesh.VtxEdgesItr(vid).Intersect(edgesIDs);
                if (parents.Count() % 2 == 1)
                    throw new Exception("Невозможно обойти граф");
                vertParentsDict.Add(vid, parents);
            }
            var bypassEdges = new List<int>(edgesIDs);
            var loops = new List<IEnumerable<int>>();
            var verticesQueue = new Queue<int>();
            List<int> currentLoopEdges = null;
            while (verticesQueue.Count > 0 ||
                verticesIds.Count > 0)
            {
                if (verticesQueue.Count < 1)
                {
                    currentLoopEdges = new List<int>();
                    loops.Add(currentLoopEdges);
                    var tempTID = verticesIds.First();
                    verticesIds.RemoveAt(0);
                    verticesQueue.Enqueue(tempTID);
                }
                var vid = verticesQueue.Dequeue();
                var parents = vertParentsDict[vid];
                if (parents.Count() == 2)
                {
                    var parent0 = parents.ElementAt(0);
                    var parent1 = parents.ElementAt(1);
                    var vtx0 = GetVertexNeigborIdByEdge(vid, parent0);
                    var vtx1 = GetVertexNeigborIdByEdge(vid, parent1);
                    if (bypassEdges.Contains(parent0))
                    {
                        currentLoopEdges.Add(parent0);
                        bypassEdges.Remove(parent0);
                        if (verticesIds.Contains(vtx0))
                        {
                            verticesQueue.Enqueue(vtx0);
                            verticesIds.Remove(vtx0);
                        }
                    }
                    if (bypassEdges.Contains(parent1))
                    {
                        currentLoopEdges.Add(parent1);
                        bypassEdges.Remove(parent1);
                        if (verticesIds.Contains(vtx1))
                        {
                            verticesQueue.Enqueue(vtx1);
                            verticesIds.Remove(vtx1);
                        }
                    }
                }
            }
            return loops;
        }
    }
}
