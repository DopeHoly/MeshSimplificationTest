using CGALDotNet;
using g3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MeshSimplificationTest.SBRep
{
    public static class SBRepBuilder
    {
        public const double EPS_PointOnPlane = 1e-3;
        public const double EPS_NormalCompare = 1e-4;
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
            public int GroupId { get; set; } = -1;
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

        public static IndexedCollection<LoopEdge> SeparateLoopEdgeByMerged(DMesh3 mesh, IndexedCollection<LoopEdge> loopEdges)
        {
            var newLoopEdges = new IndexedCollection<LoopEdge>();
            foreach (var loopEdge in loopEdges)
            {
                var edges = new List<int>(loopEdge.edgeIDs);
                var edgeQueue = new Queue<int>();
                LoopEdge currentLE = null;
                while (edgeQueue.Count > 0 ||
                       edges.Count() > 0)
                {
                    if(edgeQueue.Count < 1)
                    {
                        currentLE = new LoopEdge()
                        {
                            neigbor = loopEdge.neigbor
                        };
                        newLoopEdges.Add(currentLE);
                        var tempEID = edges.First();
                        edges.RemoveAt(0);
                        edgeQueue.Enqueue(tempEID);
                    }
                    var eid = edgeQueue.Dequeue();
                    currentLE.edgeIDs.Add(eid);
                    if (edges.Count == 0)
                        continue;

                    var edgeV = mesh.GetEdgeV(eid);
                    var neighbours = edges.Where(x =>
                    {
                        var edgeVtx = mesh.GetEdgeV(x);
                        return
                            edgeVtx.a == edgeV.a ||
                            edgeVtx.b == edgeV.a ||
                            edgeVtx.a == edgeV.b ||
                            edgeVtx.b == edgeV.b;
                    }).ToList();
                    foreach (var neigbour in neighbours)
                    {
                        edgeQueue.Enqueue(neigbour);
                        edges.Remove(neigbour);
                    }
                }
            }

            return newLoopEdges;
        }

        public static IndexedCollection<LoopEdge> GroupEdgeByPlaneIntersection(DMesh3 mesh, IEnumerable<TriPlanarGroup> planarGroups)
        {
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
                if (edgeTri.a == -1 || edgeTri.b == -1)
                    throw new Exception("Объект не замкнут");
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

        public static IndexedCollection<LoopEdge> BuildLooppart(DMesh3 mesh, IEnumerable<TriPlanarGroup> planarGroups)
        {
            var loopEdges = GroupEdgeByPlaneIntersection(mesh, planarGroups);
            //тут нужно разделить рёбра по зонам связанности и отсортировать рёбра по порядку
            loopEdges = SeparateLoopEdgeByMerged(mesh, loopEdges);

            return loopEdges;
        }

        private static bool findLoop(IEnumerable<SBRep_Loop> loops, SBRep_Loop newLoop, ref int id)
        {
            id = -1;

            foreach(var loop in loops)
            {
                if (loop.LoopEdges.Count != newLoop.LoopEdges.Count)
                    continue;
                var edgesA = loop.LoopEdges;
                var edgesB = newLoop.LoopEdges;
                var identical = edgesA.All(x => edgesB.Contains(x));
                if (identical)
                {
                    id = loop.ID;
                    return true;
                }
            }

            return false;
        }

        public static Tuple<IEnumerable<SBRep_Loop>, IDictionary<int, IEnumerable<int>>> BuildLoops(
            IEnumerable<TriPlanarGroup> planarGroups,
            IEnumerable<LoopEdge> loopparts)
        {
            //var newLoops = new IndexedCollection<SBRep_Loop>();
            var edgeLP = new Dictionary<int, int>();
            foreach (var looppart in loopparts)
            {
                foreach (var eid in looppart.edgeIDs)
                {
                    edgeLP.Add(eid, looppart.ID);
                }
            }

            var resultLoops = new IndexedCollection<SBRep_Loop>();
            var planarLoopsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var planarGroup in planarGroups)
            {
                var loops = planarGroup.GetLoops();

                var planesLoopsDict = new Dictionary<int, int>();
                var currentPlanarloops = new List<int>();
                foreach (var loop in loops)
                {
                    //todo если три точки практически лежат на петле, то и  пропускаем её
                    var loopPartIDs = new List<int>();
                    foreach (var edge in loop.Edges)
                    {
                        loopPartIDs.Add(edgeLP[edge]);
                    }
                    loopPartIDs = loopPartIDs.Distinct().ToList();

                    int loopId = -1;
                    var newLoop = new SBRep_Loop()
                    {
                        LoopEdges = loopPartIDs,
                    };
                    if (!findLoop(resultLoops, newLoop, ref loopId))
                    {
                        resultLoops.Add(newLoop);
                        loopId = newLoop.ID;
                    }
                    //resultLoops.Add(newLoop);
                    currentPlanarloops.Add(loopId);
                }
                currentPlanarloops = currentPlanarloops.Distinct().ToList();
                planarLoopsDict.Add(planarGroup.ID, currentPlanarloops);
            }

            return new Tuple<IEnumerable<SBRep_Loop>, IDictionary<int, IEnumerable<int>>>(resultLoops, planarLoopsDict);
        }
        public static IndexedCollection<SBRep_Face> BuildFaces(
            IEnumerable<TriPlanarGroup> planarGroups,
            IEnumerable<SBRep_Loop> loops,
            IDictionary<int, IEnumerable<int>> planarsLoops)
        {
            var faces = new IndexedCollection<SBRep_Face>();
            foreach (var face in planarGroups)
            {
                var allLoopsIDs = planarsLoops[face.ID];
                //Tuple<int, List<int>> insideOutsideLoops = null;
                faces.Add(new SBRep_Face()
                {
                    ID = face.ID,
                    Normal = face.Normal,
                    GroupID = face.GroupId,
                    //OutsideLoop = insideOutsideLoops.Item1,
                    InsideLoops = allLoopsIDs.ToList()
                });
            }
            return faces;
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
            var faces = BuildFaces(planarGroups, loops.Item1, loops.Item2);


            var edgesIDs = planarGroups.SelectMany(x => x.GetBoundaryEdges()).Distinct();
            foreach (var eid in edgesIDs)
            {
                edgesDict.Add(eid, mesh.GetEdgeV(eid));
            }

            var vtxIds = new List<int>();
            foreach (var edge in edgesDict)
            {
                var edgeV = edge.Value;
                vtxIds.Add(edgeV.a);
                vtxIds.Add(edgeV.b);
            }
            var vtxs = vtxIds.Distinct();
            foreach (var vid in vtxs)
            {
                verticeDict.Add(vid, mesh.GetVertex(vid));
            }

            var sbRepObject = new SBRepObject();

            foreach (var vtx in verticeDict)
                sbRepObject._vertices.Add(new SBRep_Vtx()
                {
                    ID = vtx.Key,
                    Coordinate = vtx.Value,
                });

            foreach (var edge in edgesDict)
                sbRepObject._edges.Add(new SBRep_Edge()
                {
                    ID = edge.Key,
                    Vertices = edge.Value,
                });

            foreach (var loopEdge in loopparts)
                sbRepObject._loopPart.Add(new SBRep_LoopEdge()
                {
                    ID = loopEdge.ID,
                    Edges = loopEdge.edgeIDs,
                });

            foreach (var loop in loops.Item1)
                sbRepObject._loops.Add(new SBRep_Loop()
                {
                    ID = loop.ID,
                    LoopEdges = loop.LoopEdges
                });
            foreach (var face in faces)
            {
                sbRepObject._faces.Add(new SBRep_Face()
                {
                    ID = face.ID,
                    OutsideLoop = face.OutsideLoop,
                    GroupID = face.GroupID,
                    Normal = face.Normal,
                    InsideLoops = face.InsideLoops,
                });
            }
            sbRepObject.RedefineFeedbacks();
            sbRepObject.DefineFacesOutsideLoop();
            return sbRepObject;
        }
    }
}
