using g3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest.SBRep
{
    public static class SBRepBuilder
    {
        private const double EPS_PointOnPlane = 1e-3;
        private const double EPS_NormalCompare = 1e-4;
        //private const double EPS_PointOnPlane = 1e-9;
        //private const double EPS_NormalCompare = 1e-8;

        /// <summary>
        /// Представляет функцию вида Ax + By + Cz + D = 0
        /// </summary>
        public struct PlaneFace
        {
            public double A, B, C, D;
            public Vector3d Normal;

            //https://ru.onlinemschool.com/math/library/analytic_geometry/p_plane/#:~:text=Расстояние%20от%20точки%20до%20плоскости%20—%20равно%20длине%20перпендикуляра,опущенного%20из%20точки%20на%20плоскость.
            public double Distance(Vector3d point)
            {
                var dividend = Math.Abs(A * point.x + B * point.y + C * point.z + D);
                var devider = Math.Sqrt(A * A + B * B + C * C);
                return dividend / devider;
            }

            public bool PointOnPlane(Vector3d point)
            {
                return Distance(point) < EPS_PointOnPlane;
            }

            //http://algolist.ru/maths/geom/equation/plane.php
            public static PlaneFace FromPoints(
                Vector3d p1,
                Vector3d p2,
                Vector3d p3,
                Vector3d normal)
            {
                double a, b,c, d;
                a = p1.y * (p2.z - p3.z) + p2.y * (p3.z - p1.z) + p3.y * (p1.z - p2.z);
                b = p1.z * (p2.x - p3.x) + p2.z * (p3.x - p1.x) + p3.z * (p1.x - p2.x);
                c = p1.x * (p2.y - p3.y) + p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y);
                d = (p1.x*(p2.y * p3.z - p3.y * p2.z) + p2.x* (p3.y * p1.z - p1.y * p3.z) + p3.x* (p1.y* p2.z - p2.y* p1.z)) * -1.0;

                return new PlaneFace()
                {
                    A = a,
                    B = b,
                    C = c,
                    D = d,
                    Normal = normal
                };
            }
        }

        /// <summary>
        /// Представляет собой группу треугольников из mesh
        /// Все треугольники должны лежать в одной плоскости
        /// </summary>
        public class TriPlanarGroup : IIndexed
        {
            public int ID { get; set; } = -1;
            public DMesh3 mesh;
            public Vector3d Normal;
            public int GroupId = -1;
            public List<int> TriangleIDs = new List<int>();

            public TriPlanarGroup(DMesh3 mesh)
            {
                this.mesh = mesh;
            }

            public List<int> GetBoundaryEdges()
            {
                var bondaryEdgeIDs = new List<int>();
                foreach (var tid in TriangleIDs)
                {
                    var edges = mesh.GetTriEdges(tid);

                    for(int i = 0; i <= 2; ++i)
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
            public bool IsValid()
            {
                if(TriangleIDs == null || TriangleIDs.Count < 1) return false;
                var fTID = TriangleIDs.First();
                Vector3d v0 = Vector3d.Zero;
                Vector3d v1 = Vector3d.Zero;
                Vector3d v2 = Vector3d.Zero;
                mesh.GetTriVertices(fTID, ref v0, ref v1, ref v2);
                var plane = PlaneFace.FromPoints(v0, v1, v2, Normal);
                foreach (var tid in TriangleIDs)
                {
                    mesh.GetTriVertices(tid, ref v0, ref v1, ref v2);
                    var result =
                        plane.PointOnPlane(v0) &&
                        plane.PointOnPlane(v1) &&
                        plane.PointOnPlane(v2);
                    if(!result) return false;
                }

                return true;
            }

            public MeshRegionBoundaryLoops GetLoops()
            {
                //var loops = new List<Loop>();
                var edges = GetBoundaryEdges();
                MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(mesh, TriangleIDs.ToArray());
                
#if DEBUG
                foreach (var loop in loops)
                {
                    if (!loop.Edges.All(x => edges.Contains(x)))
                        throw new Exception();
                }
#endif
                return loops;
            }

            public void ClassifyLoops()
            {
                var loops = GetLoops();
                var dict = new Dictionary<int, List<int>>();
                foreach (var loop in loops)
                {
                }
            }
        }

        private static bool Vector3dEqual(Vector3d a, Vector3d b)
        {
            return Math.Abs(a.x - b.x) < EPS_NormalCompare &&
                Math.Abs(a.y - b.y) < EPS_NormalCompare &&
                Math.Abs(a.z - b.z) < EPS_NormalCompare;
        }

        /// <summary>
        /// разделение треугольников на группы по направлению нормали
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Dictionary<Vector3d, List<int>> GetTriangleIndexesGroupedByNormal(DMesh3 mesh)
        {
            var normalGroupedTIDs = new Dictionary<Vector3d, List<int>>();
            foreach (var tid in mesh.TriangleIndices())
            {
                var normal = mesh.GetTriNormal(tid);
                //вариант разбиения с точной проверкой нормали
                /*if (!normalGroupedTIDs.ContainsKey(normal))
                    normalGroupedTIDs.Add(normal, new List<int>());
                normalGroupedTIDs[normal].Add(tid);*/

                //вариант разбиения с проверкой нормали с погрешностью EPS
                //TODO - оптимизировать
                var tmpCollection = normalGroupedTIDs.Keys.Where(x => Vector3dEqual(x, normal));
                if (tmpCollection.Count() < 1)
                    normalGroupedTIDs.Add(normal, new List<int>());
                normalGroupedTIDs[tmpCollection.First()].Add(tid);

            }
            return normalGroupedTIDs;
        }

        /// <summary>
        /// разделение треугольников на объеденённые грани
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="normalGroupedTIDs"></param>
        /// <returns></returns>
        public static IEnumerable<TriPlanarGroup> SeparateByMergedGroups(
            DMesh3 mesh, 
            Dictionary<Vector3d, List<int>> normalGroupedTIDs)
        {
            var facesTriGroup = new IndexedCollection<TriPlanarGroup>();
            foreach (var triOnPlaneGroup in normalGroupedTIDs)
            {
                var normal = triOnPlaneGroup.Key;
                var triangles = new List<int>(triOnPlaneGroup.Value);
                var triangleQueue = new Queue<int>();
                ///по факту тут происходит обход в глубину по треугольникам, если очередь
                ///пустая, то соседних треугольников больше нет, если есть ещё треугольники в списке
                ///triangles, значит остались ещё грани объекта с такой же нормалью, не связанные
                ///с другими
                TriPlanarGroup currentTriPlanarGroup = null;
                while (triangleQueue.Count > 0 ||
                triangles.Count() > 0)
                {
                    if (triangleQueue.Count < 1)
                    {
                        currentTriPlanarGroup = new TriPlanarGroup(mesh)
                        {
                            Normal = normal,
                        };
                        facesTriGroup.Add(currentTriPlanarGroup);
                        var tempTID = triangles.First();
                        triangles.RemoveAt(0);
                        triangleQueue.Enqueue(tempTID);
                    }
                    var tid = triangleQueue.Dequeue();
                    currentTriPlanarGroup.TriangleIDs.Add(tid);
                    if (triangles.Count == 0)
                        continue;
                    var neighbours = mesh.GetTriNeighbourTris(tid);
                    for (int i = 0; i <= 2; ++i)
                    {
                        var nTID = neighbours[i];
                        if (triangles.Contains(nTID))
                        {
                            triangleQueue.Enqueue(nTID);
                            triangles.Remove(nTID);
                        }
                    }
                }
            }
            return facesTriGroup;
        }

        /// <summary>
        /// разделение треугольников на грани с учётом GroupID
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="facesTriGroup"></param>
        /// <returns></returns>
        public static IEnumerable<TriPlanarGroup> SeparateByGroupID(
            DMesh3 mesh,
            IEnumerable<TriPlanarGroup> facesTriGroup)
        {
            var colorizedFacesTriGroup = new IndexedCollection<TriPlanarGroup>();
            //разделение треугольников на грани с учётом GroupID
            foreach (var item in facesTriGroup)
            {
                var triangles = item.TriangleIDs;
                var groupedTri = new Dictionary<int, List<int>>();
                //формируем словарь id группы - список id треугольников, у которых такой id группы
                foreach (var tid in triangles)
                {
                    var gid = mesh.GetTriangleGroup(tid);
                    if (!groupedTri.ContainsKey(gid))
                    {
                        groupedTri.Add(gid, new List<int>());
                    }
                    groupedTri[gid].Add(tid);
                }
                foreach (var groupTri in groupedTri)
                {
                    colorizedFacesTriGroup.Add(new TriPlanarGroup(mesh)
                    {
                        Normal = item.Normal,
                        GroupId = groupTri.Key,
                        TriangleIDs = groupTri.Value,
                    });
                }
            }
            return colorizedFacesTriGroup;
        }

        public static IEnumerable<TriPlanarGroup> BuildPlanarGroups(DMesh3 mesh)
        {
            if (mesh == null)
                return null;
            //TODO опциональная, понять насколько тут это нужно
            //RemeshTool.FixAndRepairMesh(mesh);

            var normalGroupedTIDs = GetTriangleIndexesGroupedByNormal(mesh);

            var facesTriGroup = SeparateByMergedGroups(mesh, normalGroupedTIDs);

            var planes = SeparateByGroupID(mesh, facesTriGroup);

            return planes;
        }

        public static IndexedCollection<LoopEdge> BuildLooppart(DMesh3 mesh, IEnumerable<TriPlanarGroup> planarGroups)
        {
            ////индексация списка плоскостей
            //var planarGroupsMarks = new Dictionary<int, TriPlanarGroup>();
            //var cnt = 0;
            //foreach (var item in planarGroups)
            //{
            //    planarGroupsMarks.Add(cnt, item);
            //    ++cnt;
            //}

            //маркировка треугольников по принадлежности к определённой плоскости
            var triMarks = new Dictionary<int, int>();
            foreach (var group in planarGroups)
            {
                foreach (var tid in group.TriangleIDs)
                {
                    triMarks.Add(tid, group.ID);
                }
            }

            //группировка edge по одинаковым соседям
            var edgeMarks = new Dictionary<Index2i, List<int>>();

            var meshEdges = planarGroups.SelectMany(x => x.GetBoundaryEdges()).Distinct();
            foreach (var eid in meshEdges)
            {
                var edgeTri = mesh.GetEdgeT(eid);
                var t1Index = triMarks[edgeTri.a];
                var t2Index = triMarks[edgeTri.b];
                if (t1Index == t2Index)
                    throw new Exception("Нарушение целостности групп");
                //создаём пару с индексами соседних граней ребра
                var neighborIndexes = new Index2i(
                    Math.Min(t1Index, t2Index),
                    Math.Max(t1Index, t2Index));

                if (!edgeMarks.ContainsKey(neighborIndexes))
                {
                    edgeMarks.Add(neighborIndexes, new List<int>());
                }
                edgeMarks[neighborIndexes].Add(eid);                
            }
            var loopEdges = new IndexedCollection<LoopEdge>();
            foreach (var item in edgeMarks)
            {
                loopEdges.Add(new LoopEdge(item.Value, item.Key));
            }
            return loopEdges;
        }

        public static IndexedCollection<SBRep_Loop> BuildLoops(
            IEnumerable<TriPlanarGroup> planarGroups,
            IEnumerable<LoopEdge> loopparts)
        {
            var newLoops = new IndexedCollection<SBRep_Loop>();
            var edgeLP = new Dictionary<int, int>();
            foreach (var looppart in loopparts)
            {
                foreach (var eid in looppart.edgeIDs)
                {
                    edgeLP.Add(eid, looppart.ID);
                }
            }

            var resultLoops = new IndexedCollection<SBRep_Loop>();

            foreach (var planarGroup in planarGroups)
            {
                var loops = planarGroup.GetLoops();

                var planesLoopsDict = new Dictionary<int, int>();

                foreach (var loop in loops)
                {
                    var tempEdgeList = new List<int>();
                    foreach (var edge in loop.Edges)
                    {
                        tempEdgeList.Add(edgeLP[edge]);
                    }
                    tempEdgeList = tempEdgeList.Distinct().ToList();
                    var newLoop = new SBRep_Loop()
                    {
                        Edges = tempEdgeList,
                    };
                    resultLoops.Add(newLoop);
                }
            }

            return null;
        }
        public static IndexedCollection<SBRep_Face> BuildFaces(
            IEnumerable<TriPlanarGroup> planarGroups,
            IndexedCollection<SBRep_Loop> loops)
        {
            return null;
        }

        //public static void ReindexData()

        public static SBRepObject Convert(DMesh3 mesh)
        {
            if (mesh == null)
                return null;

            var planarGroups = BuildPlanarGroups(mesh);

            var verticeDict = new Dictionary<int, Vector3d>();
            var edgesDict = new Dictionary<int, Index2i>();
            var loopparts = BuildLooppart(mesh, planarGroups);
            var loops = BuildLoops(planarGroups, loopparts);
            var buildFaces = BuildFaces(planarGroups, loops);



            return null;
        }
    }
}
