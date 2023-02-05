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

            /// <summary>
            /// расстояние от точки до плоскости
            /// https://ru.onlinemschool.com/math/library/analytic_geometry/p_plane/#:~:text=Расстояние%20от%20точки%20до%20плоскости%20—%20равно%20длине%20перпендикуляра,опущенного%20из%20точки%20на%20плоскость.
            /// </summary>
            /// <param name="point"></param>
            /// <returns></returns>
            public double Distance(Vector3d point)
            {
                var dividend = Math.Abs(A * point.x + B * point.y + C * point.z + D);
                var devider = Math.Sqrt(A * A + B * B + C * C);
                return dividend / devider;
            }

            /// <summary>
            /// Точка на плоскости
            /// </summary>
            /// <param name="point"></param>
            /// <returns></returns>
            public bool PointOnPlane(Vector3d point)
            {
                return Distance(point) < EPS_PointOnPlane;
            }

            public double GetZ(double x, double y)
            {
                return (-A * x - B * y - D) / C;
            }

            /// <summary>
            /// Создаёт уравнение плоскости по трём точкам
            /// http://algolist.ru/maths/geom/equation/plane.php
            /// </summary>
            /// <param name="p1"></param>
            /// <param name="p2"></param>
            /// <param name="p3"></param>
            /// <param name="normal"></param>
            /// <returns></returns>
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
                d = (p1.x * (p2.y * p3.z - p3.y * p2.z) + p2.x * (p3.y * p1.z - p1.y * p3.z) + p3.x * (p1.y * p2.z - p2.y * p1.z)) * -1.0;

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
                    if(!result) return false;
                }

                return true;
            }

            /// <summary>
            /// Возвращает петли
            /// </summary>
            /// <returns>MeshRegionBoundaryLoops - класс из g3 либы, я не совсем уверен, что он в 100 прцентах случаев делает то, что нужно</returns>
            public MeshRegionBoundaryLoops GetLoops()
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
                return loops;
            }
        }

        /// <summary>
        /// Функция проверки равнозначности векторов
        /// Проверяет с погрешностью EPS_NormalCompare
        /// </summary>
        /// <param name="a">Вектор первый</param>
        /// <param name="b">Вектор второй</param>
        /// <returns></returns>
        private static bool Vector3dEqual(Vector3d a, Vector3d b)
        {
            return Math.Abs(a.x - b.x) < EPS_NormalCompare &&
                Math.Abs(a.y - b.y) < EPS_NormalCompare &&
                Math.Abs(a.z - b.z) < EPS_NormalCompare;
            //return a.EpsilonEqual(b, EPS_NormalCompare);
            //return a.Equals(b);
        }

        /// <summary>
        /// разделение треугольников на группы по направлению нормали
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Dictionary<Vector3d, List<int>> GroupTriangleByNormal(DMesh3 mesh)
        {
            var normalGroupedTIDs = new Dictionary<Vector3d, List<int>>();
            var normalTriDict = new Dictionary<int, Vector3d>();
            //foreach (var tid in mesh.TriangleIndices())
            //{
            //    normalTriDict.Add(tid, mesh.GetTriNormal(tid));
            //}
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
        public static IEnumerable<TriPlanarGroup> SeparateByMergedTri(
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

        /// <summary>
        /// Собирает группы плоских треугольников
        /// </summary>
        /// <param name="mesh">исходный объект треугольной сетки</param>
        /// <returns>список групп плоских треугольников</returns>
        public static IEnumerable<TriPlanarGroup> BuildPlanarGroups(DMesh3 mesh)
        {
            if (mesh == null)
                return null;
            //TODO опциональная, понять насколько тут это нужно
            //RemeshTool.FixAndRepairMesh(mesh);

            var normalGroupedTIDs = GroupTriangleByNormal(mesh);

            var facesTriGroup = SeparateByMergedTri(mesh, normalGroupedTIDs);

            var planes = SeparateByGroupID(mesh, facesTriGroup);

            return planes;
        }

        /// <summary>
        /// Разделяет индексированный список ломанных на связанные области
        /// </summary>
        /// <param name="mesh">исходный объект треугольной сетки</param>
        /// <param name="loopEdges">индексированный список ломанных</param>
        /// <returns>Индексированный список ломанных, каждая из которых разделяет две плоскости и разделённые на зоны связанности</returns>
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

        /// <summary>
        /// Собирает ломанные из отрезков в группы по одинаковым плоскостям, которые разделяет отрезок
        /// </summary>
        /// <param name="mesh">исходный объект треугольной сетки</param>
        /// <param name="planarGroups">группы плоскостей треугольной сетки</param>
        /// <returns>Индексированный список ломанных, каждая из которых разделяет две плоскости</returns>
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

        /// <summary>
        /// Собирает ломанные
        /// </summary>
        /// <param name="mesh">исходный объект треугольной сетки</param>
        /// <param name="planarGroups">группы плоскостей треугольной сетки</param>
        /// <returns>Индексированный список ломанных</returns>
        public static IndexedCollection<LoopEdge> BuildVerges(DMesh3 mesh, IEnumerable<TriPlanarGroup> planarGroups)
        {
            var loopEdges = GroupEdgeByPlaneIntersection(mesh, planarGroups);
            //тут нужно разделить рёбра по зонам связанности и отсортировать рёбра по порядку
            loopEdges = SeparateLoopEdgeByMerged(mesh, loopEdges);

            return loopEdges;
        }

        /// <summary>
        /// Ищет в списке loops петлю с идентичными использованными ломанными, как в newLoop
        /// </summary>
        /// <param name="loops">Список петель</param>
        /// <param name="newLoop">Петля</param>
        /// <param name="id">id найденной петли, -1, если не нашёл</param>
        /// <returns>петля с аналогичными использованными ломанными найдена</returns>
        private static bool findLoop(IEnumerable<SBRep_Loop> loops, SBRep_Loop newLoop, ref int id)
        {
            id = -1;

            foreach(var loop in loops)
            {
                if (loop.Verges.Count != newLoop.Verges.Count)
                    continue;
                var edgesA = loop.Verges;
                var edgesB = newLoop.Verges;
                var identical = edgesA.All(x => edgesB.Contains(x));
                if (identical)
                {
                    id = loop.ID;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Собирает петли и индексирует их по принадлежности к плоскостям
        /// </summary>
        /// <param name="planarGroups"></param>
        /// <param name="loopparts"></param>
        /// <returns></returns>
        public static Tuple<IEnumerable<SBRep_Loop>, IDictionary<int, IEnumerable<int>>> BuildLoops(
            IEnumerable<TriPlanarGroup> planarGroups,
            IEnumerable<LoopEdge> loopparts)
        {
            //индексация какое ребро к какой ломанной принадлежит
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
                        Verges = loopPartIDs,
                    };
                    if (!findLoop(resultLoops, newLoop, ref loopId))
                    {
                        resultLoops.Add(newLoop);
                        loopId = newLoop.ID;
                    }
                    currentPlanarloops.Add(loopId);
                }
                currentPlanarloops = currentPlanarloops.Distinct().ToList();
                planarLoopsDict.Add(planarGroup.ID, currentPlanarloops);
            }

            return new Tuple<IEnumerable<SBRep_Loop>, IDictionary<int, IEnumerable<int>>>(resultLoops, planarLoopsDict);
        }

        /// <summary>
        /// Собирает индексированный список плоскостей SBRep
        /// </summary>
        /// <param name="planarGroups">плоскости исходного объекта</param>
        /// <param name="planarsLoops">словарь с использованными для каждой плоскости индексами петель</param>
        /// <returns>индексированный список плоскостей SBRep</returns>
        public static IndexedCollection<SBRep_Face> BuildFaces(
            IEnumerable<TriPlanarGroup> planarGroups,
            IDictionary<int, IEnumerable<int>> planarsLoops)
        {
            var faces = new IndexedCollection<SBRep_Face>();
            foreach (var face in planarGroups)
            {
                var allLoopsIDs = planarsLoops[face.ID];
                faces.Add(new SBRep_Face()
                {
                    ID = face.ID,
                    Normal = face.Normal,
                    GroupID = face.GroupId,
                    InsideLoops = allLoopsIDs.ToList(),
                    Plane = face.GetPlane(),
                });
            }
            return faces;
        }

        /// <summary>
        /// Выделение из плоскостей объекта список граничных отрезков
        /// </summary>
        /// <param name="planarGroups">список плоскостей объекта mesh</param>
        /// <returns>Словарь (id отрезка, индексы вершин)</returns>
        public static Dictionary<int, Index2i> GetEdgesFromPlanarGroups(IEnumerable<TriPlanarGroup> planarGroups)
        {
            var mesh = planarGroups.First().mesh;
            var edgesDict = new Dictionary<int, Index2i>();
            var edgesIDs = planarGroups.SelectMany(x => x.GetBoundaryEdges()).Distinct();
            foreach (var eid in edgesIDs)
            {
                edgesDict.Add(eid, mesh.GetEdgeV(eid));
            }
            return edgesDict;
        }

        /// <summary>
        /// Выделение вершин из отрезков, представленных список из использованных вершин
        /// </summary>
        /// <param name="mesh">объект треугольной сетки</param>
        /// <param name="edges">список рёбер, заданный двумя индексами вершин</param>
        /// <returns>Словарь (id вершины, координата)</returns>
        public static Dictionary<int, Vector3d> GetVerticeFromMeshEdge(DMesh3 mesh, IEnumerable<Index2i> edges)
        {
            var verticeDict = new Dictionary<int, Vector3d>();
            var vtxIds = new List<int>();
            foreach (var edge in edges)
            {
                var edgeV = edge;
                vtxIds.Add(edgeV.a);
                vtxIds.Add(edgeV.b);
            }
            var vtxs = vtxIds.Distinct();
            foreach (var vid in vtxs)
            {
                verticeDict.Add(vid, mesh.GetVertex(vid));
            }
            return verticeDict;
        }

        /// <summary>
        /// Конвертирует объект треугольной сетки в объект SBRepObject
        /// </summary>
        /// <param name="mesh">объект треугольной сетки</param>
        /// <returns>объект SBRepObject</returns>
        public static SBRepObject Convert(DMesh3 mesh)
        {
            if (mesh == null)
                return null;

            ///Выделяем из исходного объекта группы связанных треугольников
            ///лежащих на одной плоскости и имеющие одинаковый GroupID
            var planarGroups = BuildPlanarGroups(mesh);

            //Получаем словарь Id/Index2i(индексы на точки) из выделеенных групп треугольников
            var edgesDict = GetEdgesFromPlanarGroups(planarGroups);

            //Формируем из списка использованных граней использованные вершины
            var verticeDict = GetVerticeFromMeshEdge(mesh, edgesDict.Values);
            //Собираем ломанные
            var loopparts = BuildVerges(mesh, planarGroups);
            //Собираем петли
            var loops = BuildLoops(planarGroups, loopparts);
            //Собираем плоскости
            var faces = BuildFaces(planarGroups,loops.Item2);

            var sbRepObject = new SBRepObject();

            foreach (var vtx in verticeDict)
                sbRepObject.Vertices.Add(new SBRep_Vtx()
                {
                    ID = vtx.Key,
                    Coordinate = vtx.Value,
                });

            foreach (var edge in edgesDict)
                sbRepObject.Edges.Add(new SBRep_Edge()
                {
                    ID = edge.Key,
                    Vertices = edge.Value,
                });

            foreach (var loopEdge in loopparts)
                sbRepObject.Verges.Add(new SBRep_Verge()
                {
                    ID = loopEdge.ID,
                    Edges = loopEdge.edgeIDs,
                });

            foreach (var loop in loops.Item1)
                sbRepObject.Loops.Add(new SBRep_Loop()
                {
                    ID = loop.ID,
                    Verges = loop.Verges
                });

            foreach (var face in faces) 
                sbRepObject.Faces.Add(new SBRep_Face()
                {
                    ID = face.ID,
                    OutsideLoop = face.OutsideLoop,
                    GroupID = face.GroupID,
                    Normal = face.Normal,
                    InsideLoops = face.InsideLoops,
                    Plane = face.Plane,
                });

            //Индексирует обратные связи между разными объектами
            sbRepObject.RedefineFeedbacks();
            //Разделяет внешние и внутренние грани у плоскостей
            sbRepObject.DefineFacesOutsideLoop();
            return sbRepObject;
        }
    }
}
