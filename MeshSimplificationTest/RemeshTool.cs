using gs;
using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeshSimplificationTest
{
    /// <summary>
    /// Класс для перестроения сетки с учётом параметров
    /// основная информация получена с сайта
    /// http://www.gradientspace.com/tutorials/2018/7/5/remeshing-and-constraints
    /// </summary>
    public class RemeshTool
    {
        /// <summary>
        /// Целевая длина ребра треугольника будущего меша
        /// </summary>
        public double EdgeLength { get; set; }

        /// <summary>
        /// Коэффициент размытия. В пределах [0; 1]
        /// </summary>
        public double SmoothSpeed { get; set; }

        /// <summary>
        /// Включает ограничение на взаимодействие с гранями, 
        /// которые имеют между собой угол больше, чем пороговый
        /// </summary>
        public bool KeepAngle { get; set; }

        /// <summary>
        /// Пороговая величина внешнего угла между гранями
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// Количество прогонов ремеша
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Включить переворот граней
        /// </summary>
        public bool EnableFlips { get; set; }

        /// <summary>
        /// Включить сворачивание граней
        /// </summary>
        public bool EnableCollapses { get; set; }

        /// <summary>
        /// Включить разделение граней
        /// </summary>
        public bool EnableSplits { get; set; }

        /// <summary>
        /// Включить сглаживание
        /// </summary>
        public bool EnableSmoothing { get; set; }

        /// <summary>
        /// Включить ограничения для граничного контура групп треугольников (face group)
        /// </summary>
        public bool EnableFaceGroup { get; set; }

        /// <summary>
        /// Разрешить разрушать точки в ограниченных контурах
        /// </summary>
        public bool AllowCollapseFixedVertsWithSameSetID { get; set; }

        public Remesher.TargetProjectionMode TargetProjectionMode { get; set; }

        /// <summary>
        /// Тип сглаживания
        /// </summary>
        public Remesher.SmoothTypes SmoothType { get; set; }

        /// <summary>
        /// Включить репроекцию объёма объекта
        /// Ремешер будет стараться сохранить прежний объём объекта
        /// </summary>
        public bool Reprojection { get; set; }


        public RemeshTool()
        {
            SetDefaultSettings();
        }

        private void SetDefaultSettings()
        {
            EnableFlips = true;
            EnableCollapses = true;
            EnableSplits = true;
            EnableSmoothing = true;
            KeepAngle = true;
            Angle = 3;
            SmoothSpeed = 0.5;
            EdgeLength = 1;
            Iterations = 25;
            EnableFaceGroup = true;
            AllowCollapseFixedVertsWithSameSetID = true;
            TargetProjectionMode = Remesher.TargetProjectionMode.Inline;
            SmoothType = Remesher.SmoothTypes.Uniform;
            Reprojection = true;
        }

        /// <summary>
        /// Вернёт новую сетку в соответствии с настройками RemeshTool
        /// </summary>
        /// <param name="inputModel"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public async Task<DMesh3> RemeshAsync(DMesh3 inputModel, CancellationToken cancelToken, IProgress<double> progress = null)
        {
            return await Task.Run(() => Remesh(inputModel, cancelToken, progress));
        }

        /// <summary>
        /// Работает с предварительно настроенным классом Remesh
        /// </summary>
        /// <param name="r"></param>
        /// <param name="Iterations"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        public static async void RemeshAsync(Remesher r, int Iterations, CancellationToken cancelToken, IProgress<double> progress = null)
        {
            await Task.Run(() => RemeshCalculateIterations(r, Iterations, cancelToken, progress));
        }

        /// <summary>
        /// Вернёт новую сетку в соответствии с настройками RemeshTool
        /// </summary>
        /// <param name="inputModel"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public DMesh3 Remesh(DMesh3 inputModel, CancellationToken cancelToken = default, IProgress<double> progress = null)
        {
            var mesh = new DMesh3(inputModel);
            if (!mesh.CheckValidity(eFailMode: FailMode.ReturnOnly))
            {
                FixAndRepairMesh(mesh);
            }
            //DeleteDegenerateTriangle(mesh);

            //Remesher r = new Remesher(mesh);

            //RemesherPro это расширения класса Remesher,
            //которая должна быть эфективнее с точки зрения производительности
            RemesherPro r = new RemesherPro(mesh);

            r.PreventNormalFlips = true;
            r.Precompute();
            r.AllowCollapseFixedVertsWithSameSetID = AllowCollapseFixedVertsWithSameSetID;
            r.EnableParallelProjection = true;
            r.EnableParallelSmooth = true;
            r.EnableSmoothInPlace = true;
            r.EdgeFlipTolerance = 0.01;
            r.SmoothType = SmoothType;
            r.ProjectionMode = TargetProjectionMode;


            MeshConstraints cons = new MeshConstraints();
            EdgeRefineFlags edgeRefineFlags = EdgeRefineFlags.NoFlip;

            if (Reprojection)
            {
                r.SetProjectionTarget(MeshProjectionTarget.Auto(mesh));
                cancelToken.ThrowIfCancellationRequested();
            }

            if (EnableFaceGroup)
            {
                MeshConstraintsGroups(mesh, edgeRefineFlags, cons);
                cancelToken.ThrowIfCancellationRequested();
            }

            if (KeepAngle)
            {
                MeshConstraintsAngle(mesh, Angle, edgeRefineFlags, cons);
                cancelToken.ThrowIfCancellationRequested();
            }

            r.Precompute();
            r.SetExternalConstraints(cons);

            r.EnableFlips = EnableFlips;
            r.EnableCollapses = EnableCollapses;
            r.EnableSplits = EnableSplits;

            r.EnableSmoothing = EnableSmoothing;
            r.SmoothSpeedT = SmoothSpeed;
            r.SetTargetEdgeLength(EdgeLength);


            cancelToken.ThrowIfCancellationRequested();
            RemeshCalculateIterations(r, Iterations, cancelToken, progress);
            MeshEditor.RemoveFinTriangles(mesh);
            DeleteDegenerateTriangle(mesh);
            return mesh;
        }

        /// <summary>
        /// Расчёт сетки
        /// </summary>
        /// <param name="r"></param>
        /// <param name="interations"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        private static void RemeshCalculateIterations(Remesher r, int interations, CancellationToken cancelToken, IProgress<double> progress = null)
        {
            for (int k = 0; k < interations; ++k)
            {
                cancelToken.ThrowIfCancellationRequested();
                progress?.Report((double)k / (double)interations);
                r.BasicRemeshPass();
            }
        }

        /// <summary>
        /// Обновляет ограничения для ремеша по группам треугольников меша
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgeRefineFlags"></param>
        /// <param name="constraints"></param>
        private void MeshConstraintsGroups(DMesh3 mesh, EdgeRefineFlags edgeRefineFlags, MeshConstraints constraints)
        {
            var edgesID = GetEdgesIdConstrainsByGroups(mesh);
            MeshConstraints(mesh, edgesID, edgeRefineFlags, constraints);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private List<int> GetEdgesIdConstrainsByGroups(DMesh3 mesh)
        {
            var edgesId = new List<int>();
            int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(mesh);
            foreach (int[] tri_list in group_tri_sets)
            {
                MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(mesh, tri_list);
                foreach (EdgeLoop loop in loops)
                {
                    foreach (var eid in loop.Edges)
                    {
                        edgesId.Add(eid);
                    }
                }
            }
            edgesId = edgesId.Distinct().ToList();
            return edgesId;
        }

        /// <summary>
        /// Обновляет ограничения для ремеша по углу граней
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="angle"></param>
        /// <param name="edgeRefineFlags"></param>
        /// <param name="constraints"></param>
        private void MeshConstraintsAngle(DMesh3 mesh, double angle, EdgeRefineFlags edgeRefineFlags, MeshConstraints constraints)
        {
            var edgesID = GetEdgesIdConstrainsByAngle(mesh, angle);
            MeshConstraints(mesh, edgesID, edgeRefineFlags, constraints);
        }

        /// <summary>
        /// Вычисление граней, между которыми треугольники расположены под углом больше angle
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="angle"></param>
        /// <returns>список id граней, подпадающих под условие</returns>
        public List<int> GetEdgesIdConstrainsByAngle(DMesh3 mesh, double angle)
        {
            var edgesId = new List<int>();

            foreach (int eid in mesh.EdgeIndices())
            {
                var edgeIDs = mesh.GetEdgeV(eid);
                double fAngle = MeshUtil.OpeningAngleD(mesh, eid);
                if (fAngle > angle)
                {
                    edgesId.Add(eid);
                }
            }
            return edgesId;
        }

        /// <summary>
        /// Обновление ограничений для edgesID
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgesID"></param>
        /// <param name="edgeRefineFlags"></param>
        /// <param name="constraints"></param>
        private void MeshConstraints(DMesh3 mesh, List<int> edgesID, EdgeRefineFlags edgeRefineFlags, MeshConstraints constraints)
        {
            foreach (var eid in edgesID)
            {
                constraints.SetOrUpdateEdgeConstraint(eid, new EdgeConstraint(edgeRefineFlags));
            }

            var vtxsID = GetVtxIDStats(mesh, edgesID);
            var counter = 100000;
            var vtxValid = new List<int>();

            ///Мы можем удалять точки на гранях только если она принадлежит линии или контуру
            ///то есть имеет только 2 родительских ребра ограничения. Если выбрать другие, например
            ///с 3 ребрами, то углы куба могут быть удалены, что ведёт изменение формы
            const int validParentEdgeCount = 2;

            foreach (var item in vtxsID)
            {
                if (item.Value == validParentEdgeCount)
                {
                    vtxValid.Add(item.Key);
                }
                else
                {
                    ///выставление ограничение VertexConstraint работает так, что вершине можно присвоить индекс.
                    ///Настройка AllowCollapseFixedVertsWithSameSetID по возможности будет схлопывать грани с одинаковыми
                    ///номерами, значит выставить нужно так, что все углы должны быть помечаны уникальными номерами
                    ///таковым является counter
                    constraints.SetOrUpdateVertexConstraint(item.Key, new VertexConstraint(true, counter));
                }
                ++counter;
            }

            ///Что бы каждая отдельное ребро грани могло схлопывать точки и не перемешиваться с другими рёбрами
            ///нужно каждую пометить своим номером.
            //int borderCounter = counter + 1;
            int borderCounter = 0;
            while (vtxValid != null && vtxValid.Count > 0)
            {
                var firstVtxGroup = vtxValid.First();
                vtxValid.Remove(firstVtxGroup);

                var queue = new Queue<int>();
                queue.Enqueue(firstVtxGroup);
                constraints.SetOrUpdateVertexConstraint(firstVtxGroup, new VertexConstraint(true, borderCounter));
                do
                {
                    var curentId = queue.Dequeue();
                    var esges = edgesID.FindAll((x) =>
                    {
                        var edge = mesh.GetEdgeV(x);
                        return edge[0] == curentId || edge[1] == curentId;
                    });
                    foreach (var eid in esges)
                    {
                        //получаем вторую точку ребра
                        var edge = mesh.GetEdgeV(eid);
                        int vtx2 = edge[0] == curentId ? edge[1] : edge[0];

                        if (vtxValid.Contains(vtx2))
                        {
                            vtxValid.Remove(vtx2);
                            queue.Enqueue(vtx2);
                            constraints.SetOrUpdateVertexConstraint(vtx2, new VertexConstraint(true, borderCounter));
                        }
                    }
                } while (queue.Count > 0);

                ++borderCounter;
            }
        }

        /// <summary>
        /// Вычисление сколько раз в списке граней встречается каждая из точек
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgesId"></param>
        /// <returns>словарь(id вертекса,кол-во упоминаний)></id></returns>
        private Dictionary<int, int> GetVtxIDStats(DMesh3 mesh, List<int> edgesId)
        {
            var dict = new Dictionary<int, int>();

            foreach (var eid in edgesId)
            {
                var edge = mesh.GetEdgeV(eid);
                for (int i = 0; i < 2; ++i)
                {
                    var value = edge[i];
                    if (!dict.ContainsKey(value))
                    {
                        dict.Add(value, 0);
                    }
                    dict[value] += 1;
                }
            }

            return dict;
        }

        /// <summary>
        /// Автоматическое исправление нормалей меша
        /// </summary>
        /// <param name="mesh"></param>
        public static void FixNormals(DMesh3 mesh)
        {
            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();
        }

        /// <summary>
        /// втыкание заплаток, если потеряли часть меша
        /// </summary>
        /// <param name="mesh"></param>
        public static void RepairMash(DMesh3 mesh)
        {
            var meshRepair = new MeshAutoRepair(mesh);
            meshRepair.Apply();
        }

        /// <summary>
        /// Автоматическое исправление нормалей меша и втыкание заплаток, если потеряли часть меша
        /// </summary>
        /// <param name="mesh"></param>
        public static void FixAndRepairMesh(DMesh3 mesh)
        {
            FixNormals(mesh);
            RepairMash(mesh);
        }
        public MeshConstraints GetMeshConstraints(DMesh3 mesh)
        {
            if (!mesh.CheckValidity(eFailMode: FailMode.ReturnOnly))
            {
                FixNormals(mesh);
            }
            var cons = new MeshConstraints();
            EdgeRefineFlags edgeRefineFlags = EdgeRefineFlags.NoFlip;
            if (EnableFaceGroup)
            {
                MeshConstraintsGroups(mesh, edgeRefineFlags, cons);
            }

            if (KeepAngle)
            {
                MeshConstraintsAngle(mesh, Angle, edgeRefineFlags, cons);
            }
            ConstraintsDegenerateTri(mesh, cons);
            return cons;
        }

        public static bool IsDegenerativeTriangle(DMesh3 mesh, int tid/*, out int vtx*/)
        {
            //vtx = -1;
            //var minAngle = double.MinValue;
            //int[] indx = { 0, 1, 2 };
            //foreach (var id in indx)
            //{
            //    minAngle = Math.Max(mesh.GetTriInternalAngleR(tid, id), minAngle);
            //    //var vtx = mesh.GetTriVertex(tid, id);
            //}
            //minAngle *= 180 / Math.PI;
            //if (minAngle > 177)
            //{
            //    return true;
            //}
            var area = mesh.GetTriArea(tid);
            var areaTreshold = 0.00001;
            if (area < areaTreshold)
            {
                //var minAngle = double.MinValue;
                //int[] indx = { 0, 1, 2 };
                //foreach (var id in indx)
                //{
                //    minAngle = Math.Max(mesh.GetTriInternalAngleR(tid, id), minAngle);
                //    //var vtx = mesh.GetTriVertex(tid, id);
                //}
                //minAngle *= 180 / Math.PI;
                return true;
            }
            return false;
        }

        private static int GetDegenerateVtxId(DMesh3 mesh, int tid)
        {
            var triangle = mesh.GetTriangle(tid);
            var a = mesh.GetVertex(triangle.a);
            var b = mesh.GetVertex(triangle.b);
            var c = mesh.GetVertex(triangle.c);
            var angleA = Vector3d.AngleD(b - a, c - a);
            var angleB = Vector3d.AngleD(a - b, c - b);
            var angleC = Vector3d.AngleD(a - c, b - c);
            var maxAngle = new[] { angleA, angleB, angleC }.Max();
            if (angleA == maxAngle)
            {
                return triangle.a;
            }
            if (angleB == maxAngle)
            {
                return triangle.b;
            }
            if (angleC == maxAngle)
            {
                return triangle.c;
            }
            return -1;
        }

        private void ConstraintsDegenerateTri(DMesh3 mesh, MeshConstraints constraints)
        {
            var contspointFlag = 99901;
            foreach (int triangleid in mesh.TriangleIndices())
            {
                if (IsDegenerativeTriangle(mesh, triangleid))
                {
                    var triangle = mesh.GetTriangle(triangleid);
                    var pointId = GetDegenerateVtxId(mesh, triangleid);
                    constraints.SetOrUpdateVertexConstraint(pointId, new VertexConstraint(true, contspointFlag));
                }

            }
        }

        public static void DeleteDegenerateTriangle(DMesh3 mesh)
        {
            foreach (int triangleid in mesh.TriangleIndices())
            {
                if (IsDegenerativeTriangle(mesh, triangleid))
                {
                    var triangle = mesh.GetTriangle(triangleid);
                    var pointId = GetDegenerateVtxId(mesh, triangleid);
                    var cntTriangle = mesh.GetVtxTriangleCount(pointId);
                    Index3i trianglePoints = Index3i.Zero;
                    switch (cntTriangle)
                    {
                        //case 3:
                        //    trianglePoints = GetNewTriangleIndexes(mesh, pointId, triangleid);
                        //    break;
                        default:
                            var center = MeshWeights.OneRingCentroid(mesh, pointId);

                            //проверка, что лежит на одной плоскости
                            //if(PointOnAnyTriangles(mesh, pointId, center))
                                mesh.SetVertex(pointId, center);

                            //PrintToCsv(@"C:\GitProjects\MeshSimplificationTest\samples\Trouble\" + "trouble" + cntTriangle.ToString() + "_" + pointId.ToString() + ".csv", mesh, pointId);
                            break;
                    }
                }

            }
        }

        private static bool PointOnAnyTriangles(DMesh3 mesh, int vid, Vector3d point)
        {
            var triangles = new List<int>();
            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                return triangles.All(x => PointOnTriangle(mesh, point, x));
            }
            return false;
        }

        private static bool PointOnTriangle(DMesh3 mesh, Vector3d point, int tid)
        {
            var triangleIds = mesh.GetTriangle(tid);
            var a = mesh.GetVertex(triangleIds.a);
            var b = mesh.GetVertex(triangleIds.b);
            var c = mesh.GetVertex(triangleIds.c);
            var triangle = new Triangle3d(a, b, c);
            var distanceCalculator = new DistPoint3Triangle3(point, triangle);
            var distance = distanceCalculator.Get();
            return distance < 0.1;
        }

        private static void PrintToCsv(string path, DMesh3 mesh, int vid)
        {
            var builder = new StringBuilder();

            var triangles = new List<int>();
            char separator = ';';
            builder.Append($"id{separator}xid{separator}yid{separator}zid{separator}ax{separator}ay{separator}az{separator}bx{separator}by{separator}bz{separator}cx{separator}cy{separator}cz\n");
            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                foreach (var tid in triangles)
                {
                    var triangle = mesh.GetTriangle(tid);
                    builder.Append($"{tid}{separator}{triangle.a}{separator}{triangle.b}{separator}{triangle.c}");
                    var a = mesh.GetVertex(triangle.a);
                    var b = mesh.GetVertex(triangle.b);
                    var c = mesh.GetVertex(triangle.c);
                    builder.Append($"{separator}{a.x}{separator}{a.y}{separator}{a.z}");
                    builder.Append($"{separator}{b.x}{separator}{b.y}{separator}{b.z}");
                    builder.Append($"{separator}{c.x}{separator}{c.y}{separator}{c.z}");
                    builder.Append($"\n");
                }
            }
            System.IO.File.WriteAllText(path, builder.ToString());
        }

        private static Index3i GetNewTriangleIndexes(DMesh3 mesh, int vid, int badTriId)
        {
            var triangles = new List<int>(3);
            //Dictionary<int, Index3i> triangleIndexes = new Dictionary<int, Index3i>();
            var trianglePoints = new List<int>(9);
            var templateTriangle = Index3i.Zero;
            var gid = -1;
            
            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                foreach (var tid in triangles)
                {
                    var triangle = mesh.GetTriangle(tid);
                    if (triangle.a != vid)
                        trianglePoints.Add(triangle.a);
                    if (triangle.b != vid)
                        trianglePoints.Add(triangle.b);
                    if (triangle.c != vid)
                        trianglePoints.Add(triangle.c);
                    if (tid != badTriId)
                    {
                        templateTriangle = triangle;
                        gid = mesh.GetTriangleGroup(tid);
                    }
                    //triangleIndexes.Add(cnt, triangle);
                    //++cnt;
                }
                var points = new List<int>(trianglePoints.Distinct());
                if(points.Count != 3)
                {
                    ;
                }
                foreach (var tid in triangles)
                {
                    mesh.RemoveTriangle(tid);
                }
                
                if (vid.Equals(templateTriangle.a))
                {
                    points.Remove(templateTriangle.b);
                    points.Remove(templateTriangle.c);
                    templateTriangle.a = points.Last();
                }
                else
                if (vid.Equals(templateTriangle.b))
                {
                    points.Remove(templateTriangle.a);
                    points.Remove(templateTriangle.c);
                    templateTriangle.b = points.Last();
                }
                else
                if (vid.Equals(templateTriangle.c))
                {
                    points.Remove(templateTriangle.b);
                    points.Remove(templateTriangle.a);
                    templateTriangle.c = points.Last();
                }
                mesh.AppendTriangle(templateTriangle, gid);
                return templateTriangle;

            }
            else
            {
                ;
            }
            return new Index3i(-1, -1, -1);
        }

        public static bool HasTriangleIntersection(DMesh3 mesh)
        {
            foreach (var ti1 in mesh.TriangleIndices())
            {
                Vector3d a = Vector3d.Zero, b = Vector3d.Zero, c = Vector3d.Zero;
                mesh.GetTriVertices(ti1, ref a, ref b, ref c);
                foreach (var ti2 in mesh.TriangleIndices())
                {
                    if (ti1 == ti2) continue;
                    Vector3d e = Vector3d.Zero, f = Vector3d.Zero, g = Vector3d.Zero;
                    mesh.GetTriVertices(ti2, ref e, ref f, ref g);
                    IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(new Triangle3d(a, b, c), new Triangle3d(e, f, g));
                    if (intr.Test())
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
