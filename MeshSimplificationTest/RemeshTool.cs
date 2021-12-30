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
            Angle = 30;
            SmoothSpeed = 0.5;
            EdgeLength = 1;
            Iterations = 25;
            EnableFaceGroup = true;
            AllowCollapseFixedVertsWithSameSetID = false;
        }

        /// <summary>
        /// Вернёт новую сетку в соответствии с настройками RemeshTool
        /// </summary>
        /// <param name="inputModel"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public async Task<DMesh3> RemeshAsync(DMesh3 inputModel, CancellationToken cancelToken, IProgress<int> progress = null)
        {
            return await Task.Run(() => Calculate(inputModel, cancelToken, progress));
        }

        /// <summary>
        /// Работает с предварительно настроенным классом Remesh
        /// </summary>
        /// <param name="r"></param>
        /// <param name="Iterations"></param>
        /// <param name="cancelToken"></param>
        /// <param name="progress"></param>
        public static async void RemeshAsync(Remesher r, int Iterations, CancellationToken cancelToken, IProgress<int> progress = null)
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
        public DMesh3 Calculate(DMesh3 inputModel, CancellationToken cancelToken, IProgress<int> progress = null)
        {
            inputModel.CheckValidity();
            var mesh = new DMesh3(inputModel);
            Remesher r = new Remesher(mesh);

            r.PreventNormalFlips = true;
            r.Precompute();
            //r.AllowCollapseFixedVertsWithSameSetID = AllowCollapseFixedVertsWithSameSetID;
            r.EnableParallelProjection = false;
            r.EnableSmoothInPlace = false;
            r.SmoothType = Remesher.SmoothTypes.Cotan;

            if (EnableFaceGroup)
            {
                int set_id = 1;
                int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(mesh);
                foreach (int[] tri_list in group_tri_sets)
                {
                    MeshRegionBoundaryLoops loops = new MeshRegionBoundaryLoops(mesh, tri_list);
                    foreach (EdgeLoop loop in loops)
                    {
                        MeshConstraintUtil.ConstrainVtxLoopTo(r, loop.Vertices,
                            new DCurveProjectionTarget(loop.ToCurve()), set_id++);
                    }
                }
            }
            
            if (KeepAngle)
            {
                AxisAlignedBox3d bounds = mesh.CachedBounds;
                // construct mesh projection target
                DMesh3 meshCopy = new DMesh3(mesh);
                DMeshAABBTree3 tree = new DMeshAABBTree3(meshCopy);
                tree.Build();
                MeshProjectionTarget target = new MeshProjectionTarget()
                {
                    Mesh = meshCopy,
                    Spatial = tree
                };
                MeshConstraints cons = new MeshConstraints();
                EdgeRefineFlags useFlags = EdgeRefineFlags.NoFlip;
                foreach (int eid in mesh.EdgeIndices())
                {
                    double fAngle = MeshUtil.OpeningAngleD(mesh, eid);
                    if (fAngle > Angle)
                    {
                        cons.SetOrUpdateEdgeConstraint(eid, new EdgeConstraint(useFlags));
                        Index2i ev = mesh.GetEdgeV(eid);
                        int nSetID0 = (mesh.GetVertex(ev[0]).y > bounds.Center.y) ? 1 : 2;
                        int nSetID1 = (mesh.GetVertex(ev[1]).y > bounds.Center.y) ? 1 : 2;
                        cons.SetOrUpdateVertexConstraint(ev[0], new VertexConstraint(true, nSetID0));
                        cons.SetOrUpdateVertexConstraint(ev[1], new VertexConstraint(true, nSetID1));
                    }
                }
                r.Precompute();
                r.SetExternalConstraints(cons);
                r.SetProjectionTarget(target);
            }

            r.EnableFlips = EnableFlips;
            r.EnableCollapses = EnableCollapses;
            r.EnableSplits = EnableSplits;

            //r.MinEdgeLength = 0.9f * EdgeLength;
            //r.MaxEdgeLength = 1.1f * EdgeLength;
            r.EnableSmoothing = EnableSmoothing;
            r.SmoothSpeedT = SmoothSpeed;
            r.SetTargetEdgeLength(EdgeLength);
            r.SmoothType = Remesher.SmoothTypes.MeanValue;


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
        private static void RemeshCalculateIterations(Remesher r, int Iterations, CancellationToken cancelToken, IProgress<int> progress = null)
        {
            for (int k = 0; k < Iterations; ++k)
            {
                cancelToken.ThrowIfCancellationRequested();
                int val = (int)Math.Truncate(((double)k / (double)Iterations) * 100);
                progress?.Report(val);
                r.BasicRemeshPass();
            }
        }

        public static void SetGroupByNormal(DMesh3 mesh)
        {
            mesh.EnableTriangleGroups(1);
            mesh.EnableVertexColors(new Vector3f()
            {
                x = 0,
                y = 0,
                z = 0,
            });
            var dictionary = new Dictionary<Vector3d, int>();
            int curentGroupID = -1;
            int maxGroupId = 0;
            foreach (int triangleid in mesh.TriangleIndices())
            {
                var triangle = mesh.GetTriangle(triangleid);
                var a = mesh.GetVertex(triangle.a);
                var b = mesh.GetVertex(triangle.b);
                var c = mesh.GetVertex(triangle.c);

                var side1 = b - a;
                var side2 = c - a;
                var normal = Vector3d.Cross(side1, side2);
                if (!dictionary.ContainsKey(normal))//new group
                {
                    ++maxGroupId;
                    curentGroupID = maxGroupId;
                    dictionary[normal] = curentGroupID;
                }
                else
                {
                    curentGroupID = dictionary[normal];
                }
                mesh.SetTriangleGroup(triangleid, curentGroupID);
                var color = new Vector3f()
                {
                    x = curentGroupID * 20,
                    y = curentGroupID * 5,
                    z = curentGroupID * 6,
                };
                mesh.SetVertexColor(triangle.a, color);
                mesh.SetVertexColor(triangle.b, color);
                mesh.SetVertexColor(triangle.c, color);
            }
            return;
        }

        public static void ExportByGroup(DMesh3 mesh)
        {
            int[][] group_tri_sets = FaceGroupUtil.FindTriangleSetsByGroup(mesh);
            foreach (int[] tri_list in group_tri_sets)
            {
                
            }
        }
    }
}
