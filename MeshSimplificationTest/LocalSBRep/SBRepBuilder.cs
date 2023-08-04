using g3;
using gs;
using SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SBRep
{
    public class LoopEdge : IIndexed
    {
        public int ID { get; set; } = -1;
        public ICollection<int> edgeIDs;
        //набор индексов соседних граней объекта TriPlanarGroup
        public Index2i neigbor { get; set; }

        public LoopEdge()
        {
            edgeIDs = new List<int>();
        }
        public LoopEdge(ICollection<int> edgeIDs, Index2i neigbor)
        {
            this.neigbor = neigbor;
            this.edgeIDs = edgeIDs;

        }
    }

    public static class SBRepBuilder
    {
        public const double EPS_NormalCompare = 1e-4;
        /// <summary>
        /// Функция проверки равнозначности векторов
        /// Проверяет с погрешностью EPS_NormalCompare
        /// </summary>
        /// <param name="a">Вектор первый</param>
        /// <param name="b">Вектор второй</param>
        /// <returns></returns>
        public static bool Vector3dEqual(Vector3d a, Vector3d b)
        {
            return Math.Abs(a.x - b.x) < EPS_NormalCompare &&
                Math.Abs(a.y - b.y) < EPS_NormalCompare &&
                Math.Abs(a.z - b.z) < EPS_NormalCompare;
            //return a.EpsilonEqual(b, EPS_NormalCompare);
            //return a.Equals(b);
        }

        private static int VectorHashCodeNDigit(Vector3d vector, int digits)
        {
            int hCode =
                Math.Round(vector.x, digits).GetHashCode() ^
                Math.Round(vector.y, digits).GetHashCode() ^
                Math.Round(vector.z, digits).GetHashCode();
            return hCode;
        }
        private static Vector3d VectorNDigit(Vector3d vector, int digits)
        {
            return new Vector3d(
                Math.Round(vector.x, digits),
                Math.Round(vector.y, digits),
                Math.Round(vector.z, digits));
        }

        /// <summary>
        /// разделение треугольников на группы по направлению нормали
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Dictionary<Vector3d, List<int>> GroupTriangleByNormal(DMesh3 mesh)
        {
            var normalGroupedTIDs = new Dictionary<Vector3d, List<int>>();
            var normalTriDict = new Dictionary<Vector3d, int>();
            foreach (var tid in mesh.TriangleIndices())
            {
                var normal = VectorNDigit(mesh.GetTriNormal(tid), 4);
                if (!normalGroupedTIDs.ContainsKey(normal))
                    normalGroupedTIDs.Add(normal, new List<int>());
                normalGroupedTIDs[normal].Add(tid);
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
            //RemeshTool.FixNormals(mesh);

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

                var points = new Dictionary<int, int>();
                foreach (var eid in loopEdge.edgeIDs)
                {
                    var edgeVtx = mesh.GetEdgeV(eid);
                    var a = edgeVtx.a;
                    var b = edgeVtx.b;
                    if (!points.ContainsKey(a))
                        points.Add(a, 0);
                    if (!points.ContainsKey(b))
                        points.Add(b, 0);
                    ++points[a];
                    ++points[b];
                }
                var blackPoints = new List<int>();
                foreach (var point in points)
                {
                    if (point.Value > 2)
                        blackPoints.Add(point.Key);
                }

                var edgeQueue = new Queue<int>();
                LoopEdge currentLE = null;
                while (edgeQueue.Count > 0 ||
                       edges.Count() > 0)
                {
                    if (edgeQueue.Count < 1)
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
                        if (blackPoints.Contains(edgeVtx.a) ||
                            blackPoints.Contains(edgeVtx.b))
                            return false;
                        return
                            edgeVtx.a == edgeV.a ||
                            edgeVtx.b == edgeV.a ||
                            edgeVtx.a == edgeV.b ||
                            edgeVtx.b == edgeV.b;
                    }).ToList();
                    if (neighbours.Count > 2)
                    {
                        continue;
                    }
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
                //if (edgeTri.a == -1 || edgeTri.b == -1)
                //    throw new Exception("Объект не замкнут");
                if (edgeTri.a == -1 && edgeTri.b == -1)
                    throw new Exception("Объект не замкнут");
                int t1Index = -1;
                int t2Index = -1;
                if (triMarks.ContainsKey(edgeTri.a))
                    t1Index = triMarks[edgeTri.a];
                if (triMarks.ContainsKey(edgeTri.b))
                    t2Index = triMarks[edgeTri.b];
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

            //foreach (var loop in loops)
            //{
            //    if (loop.Verges.Count != newLoop.Verges.Count)
            //        continue;
            //    var edgesA = loop.Verges;
            //    var edgesB = newLoop.Verges;
            //    var identical = edgesA.All(x => edgesB.Contains(x));
            //    if (identical)
            //    {
            //        id = loop.ID;
            //        return true;
            //    }
            //}

            return false;
        }

        private static int LoopHash(SBRep_Loop loop)
        {
            //int hash = 0;
            //foreach (var item in loop.Verges)
            //{
            //    hash = hash ^ item;
            //}
            return loop.GetHashCode();
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
            var resultLoopsHash = new Dictionary<int, int>();
            var planarLoopsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var planarGroup in planarGroups)
            {
                var loops = planarGroup.GetLoops();

                var planesLoopsDict = new Dictionary<int, int>();
                var currentPlanarloops = new List<int>();
                foreach (var loop in loops)
                {
                    //TODO если три точки практически лежат на петле, то и  пропускаем её
                    var loopPartIDs = new List<int>();
                    foreach (var edge in loop)
                    {
                        loopPartIDs.Add(edgeLP[edge]);
                    }
                    loopPartIDs = loopPartIDs.Distinct().ToList();

                    int loopId = -1;
                    var newLoop = new SBRep_Loop()
                    {
                        Verges = loopPartIDs,
                    };
                    //TODO оптимизация
                    if (!findLoop(resultLoops, newLoop, ref loopId))
                    {
                        resultLoops.Add(newLoop);
                        loopId = newLoop.ID;
                    }
                    //var loopHash = LoopHash(newLoop);
                    //if (!resultLoopsHash.ContainsKey(loopHash))
                    //{
                    //    resultLoops.Add(newLoop);
                    //    loopId = newLoop.ID;
                    //    resultLoopsHash.Add(loopHash, loopId);
                    //}
                    //else
                    //{
                    //    loopId = resultLoopsHash[loopHash];
                    //}
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
                if (face.Normal == Vector3d.Zero)
                    continue;
                var allLoopsIDs = planarsLoops[face.ID];
                faces.Add(new SBRep_Face()
                {
                    ID = face.ID,
                    //Normal = face.Normal,
                    OutsideLoop = -1,
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
                return new SBRepObject();
            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();
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
            var faces = BuildFaces(planarGroups, loops.Item2);

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
                    //Normal = face.Normal,
                    InsideLoops = face.InsideLoops,
                    Plane = face.Plane,
                });

            //Индексирует обратные связи между разными объектами
            sbRepObject.RedefineFeedbacks();
            //Разделяет внешние и внутренние грани у плоскостей
            sbRepObject.DefineFacesOutsideLoop();
            return sbRepObject;
        }

        public static SBRepObject ConvertSimple(DMesh3 mesh)
        {
            if (mesh == null)
                return new SBRepObject();
            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();

            var sbRepObject = new SBRepObject();

            foreach (var vid in mesh.VertexIndices())
            {
                var coord = mesh.GetVertex(vid);
                sbRepObject.Vertices.Add(new SBRep_Vtx()
                {
                    ID = vid,
                    Coordinate = coord,
                });
            }

            foreach (var eid in mesh.EdgeIndices())
            {
                var vertices = mesh.GetEdgeV(eid);
                sbRepObject.Edges.Add(new SBRep_Edge()
                {
                    ID = eid,
                    Vertices = vertices,
                });
            }

            foreach (var edge in sbRepObject.Edges)
            {
                sbRepObject.Verges.Add(new SBRep_Verge()
                {
                    ID = edge.ID,
                    Edges = new List<int> { edge.ID},
                });
            }

            foreach (var tri in mesh.TriangleIndices())
            {
                var edgesIds = mesh.GetTriEdges(tri);                
                var loop = new SBRep_Loop()
                {
                    Verges = new List<int>() { edgesIds.a, edgesIds.b, edgesIds.c },
                };
                sbRepObject.Loops.Add(loop);

                Vector3d a = Vector3d.Zero;
                Vector3d b = Vector3d.Zero;
                Vector3d c = Vector3d.Zero;

                mesh.GetTriVertices(tri, ref a, ref b ,ref c);

                sbRepObject.Faces.Add(new SBRep_Face()
                {
                    GroupID = mesh.GetTriangleGroup(tri),
                    OutsideLoop = loop.ID,
                    Plane = PlaneFace.FromPoints(
                        a, b, c,
                        mesh.GetTriNormal(tri))
                });
            }                

            //Индексирует обратные связи между разными объектами
            sbRepObject.RedefineFeedbacks();
            return sbRepObject;
        }
    }
}