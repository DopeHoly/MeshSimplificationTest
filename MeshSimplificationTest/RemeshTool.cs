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
        /// Коэффициент размытия.
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
        /// Количество прогонов сглаживания
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Включить переворот граней
        /// </summary>
        public bool EnableFlips { get; set; }

        /// <summary>
        /// Включить разрушение граней
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

        public Remesher.SmoothTypes SmoothType { get; set; }

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
        public static MeshConstraints cons { get; set; }
        /// <summary>
        /// Вернёт новую сетку в соответствии с настройками RemeshTool
        /// </summary>
        /// <param name="inputModel"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public DMesh3 Remesh(DMesh3 inputModel, CancellationToken cancelToken, IProgress<double> progress = null)
        {
            if (!inputModel.CheckValidity(eFailMode: FailMode.ReturnOnly))
            {
                FixNormals(inputModel);
            }
            var mesh = new DMesh3(inputModel);
            //Remesher r = new Remesher(mesh);
            RemesherPro r = new RemesherPro(mesh);
            r.PreventNormalFlips = true;
            r.Precompute();
            r.AllowCollapseFixedVertsWithSameSetID = AllowCollapseFixedVertsWithSameSetID;
            r.EnableParallelProjection = true;
            r.EnableParallelSmooth = true;
            r.EnableSmoothInPlace = true;
            r.SmoothType = SmoothType;
            r.ProjectionMode = TargetProjectionMode;


            var cons = new MeshConstraints();
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
            //r.MinEdgeLength = EdgeLength * 0.5f;
            //r.MaxEdgeLength = EdgeLength * 1.5f;
            r.SetTargetEdgeLength(EdgeLength);


            cancelToken.ThrowIfCancellationRequested();
            RemeshCalculateIterations(r, Iterations, cancelToken, progress);
            MeshEditor.RemoveFinTriangles(mesh);
            return mesh;
        }

        /// <summary>
        /// Расчёт сетки
        /// </summary>
        /// <param name="r"></param>
        /// <param name="Iterations"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        private static void RemeshCalculateIterations(Remesher r, int Iterations, CancellationToken cancelToken, IProgress<double> progress = null)
        {
            for (int k = 0; k < Iterations; ++k)
            {
                cancelToken.ThrowIfCancellationRequested();
                double val = ((double)k / (double)Iterations) * 100;
                progress?.Report(val);
                r.BasicRemeshPass();
            }
        }

        /// <summary>
        /// Обновляет ограничения для ремеша по группам треугольников меша
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgeRefineFlags">тип ограничения граней</param>
        /// <param name="constraints">ограничения ремеша</param>
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
        /// <param name="angle">угол</param>
        /// <param name="edgeRefineFlags">тип ограничения граней</param>
        /// <param name="constraints">ограничения ремеша</param>
        private void MeshConstraintsAngle(DMesh3 mesh, double angle, EdgeRefineFlags edgeRefineFlags, MeshConstraints constraints)
        {
            var edgesID = GetEdgesIdConstrainsByAngle(mesh, angle);
            MeshConstraints(mesh, edgesID, edgeRefineFlags, constraints);
        }

        /// <summary>
        /// Вычисление граней, между которыми треугольники расположены под углом больше angle
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="angle">угол</param>
        /// <returns>список id граней, подпадающих под условие</returns>
        private List<int> GetEdgesIdConstrainsByAngle(DMesh3 mesh, double angle)
        {
            var edgesId = new List<int>();

            foreach (int eid in mesh.EdgeIndices())
            {
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
        /// <param name="edgesID">список id граней с ограничением</param>
        /// <param name="edgeRefineFlags">тип ограничения</param>
        /// <param name="constraints">ограничение</param>
        private void MeshConstraints(DMesh3 mesh, List<int> edgesID, EdgeRefineFlags edgeRefineFlags, MeshConstraints constraints)
        {
            foreach (var eid in edgesID)
            {
                constraints.SetOrUpdateEdgeConstraint(eid, new EdgeConstraint(edgeRefineFlags));
            }

            var vtxsID = GetVtxIDStats(mesh, edgesID);
            var counter = 100000;
            var vtxValid = new List<int>();
            foreach (var item in vtxsID)
            {
                var flag = counter;
                if(IsSharpVertex(mesh, edgesID, item.Key))
                {
                    constraints.SetOrUpdateVertexConstraint(item.Key, new VertexConstraint(true, flag));
                }
                else
                {

                    vtxValid.Add(item.Key);
                }
                //if (item.Value == 2)
                //{
                //    flag = 0;
                //    vtxValid.Add(item.Key);
                //}
                //constraints.SetOrUpdateVertexConstraint(item.Key, new VertexConstraint(true, flag));
                ++counter;
            }
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
        /// <param name="edgesId">список id граней</param>
        /// <returns>словарь(id вертекса,кол-во упоминаний)></id></returns>
        private Dictionary<int, int> GetVtxIDStats(DMesh3 mesh, List<int> edgesId)
        {
            var dict = new Dictionary<int, int>();

            foreach (var eid in edgesId)
            {
                var edge = mesh.GetEdgeV(eid);
                for(int i = 0; i < 2; ++i)
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

        public static void FixNormals(DMesh3 mesh)
        {
            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();
            var meshRepair = new MeshAutoRepair(mesh);
            meshRepair.Apply();
            //mesh.EnableVertexNormals(Vector3f.One);
            mesh.DiscardVertexNormals();
        }


        public MeshConstraints GetMeshConstraints(DMesh3 mesh)
        {
            if(!mesh.CheckValidity(eFailMode: FailMode.ReturnOnly))
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
            return cons;
        }

        public bool IsSharpVertex(DMesh3 mesh, List<int> edgesID, int vid)
        {
            var edges = mesh.VtxEdgesItr(vid);
            var point = mesh.GetVertex(vid);
            Vector3d sumVector = Vector3d.Zero;
            var prevLength = 0.0;
            foreach (var edge in edges)
            {
                if (!edgesID.Contains(edge)) continue;

                var vids = mesh.GetEdgeV(edge);
                var secondVtxId = vids.a == vid ? vids.b : vids.a;
                var secondVtx = mesh.GetVertex(secondVtxId);

                sumVector += secondVtx - point;
                var currentLength = sumVector.Length;
                if (currentLength < prevLength)
                    return false;
                prevLength = currentLength;
            }
            return true;
        }

        //public bool IsDegenerateTriangle(DMesh3 mesh, Index3i triangleVtxIds)
        //{
        //    var b = mesh.GetVertex(triangleVtxIds.a);
        //    var a = mesh.GetVertex(triangleVtxIds.b);
        //    var c = mesh.GetVertex(triangleVtxIds.c);
        //    vct.
        //    return false;
        //}


        //public void RemoveDegenerateTriangle(DMesh3 mesh)
        //{
        //    mesh.GetTriInternalAngleR
        //} 
    }
}
