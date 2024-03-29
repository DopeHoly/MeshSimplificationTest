﻿using gs;
using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Windows.Shapes;
using System.Windows;

namespace MeshSimplificationTest
{
    /// <summary>
    /// Класс для перестроения сетки с учётом параметров
    /// основная информация получена с сайта
    /// http://www.gradientspace.com/tutorials/2018/7/5/remeshing-and-constraints
    /// </summary>
    public class RemeshTool
    {
        public static bool UseParallelCalculations = true;


        /// <summary>
        /// хранит индекс последнего добавленного ограничения вертекса
        /// используется для избежания назначения одинаковых индексов
        /// </summary>
        private int _constraintVtxCounter = 0;


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


        /// <summary>
        /// Пороговая величина угла между гранями
        /// </summary>
        public double AngleEdge { get; set; }


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
            Angle = 1;
            SmoothSpeed = 0.5;
            EdgeLength = 1;
            Iterations = 25;
            EnableFaceGroup = true;
            AllowCollapseFixedVertsWithSameSetID = true;
            TargetProjectionMode = Remesher.TargetProjectionMode.Inline;
            SmoothType = Remesher.SmoothTypes.Uniform;
            Reprojection = true;
            AngleEdge = 180 - Angle;
        }
        /// <summary>
        /// Возращает рекомендуемую длину ребра для сетки
        /// </summary>
        public static double GetTargetEdgeLength(DMesh3 mesh)
        {
            double mine, maxe, avge;
            MeshQueries.EdgeLengthStats(mesh, out mine, out maxe, out avge);

            var coefList = new ConcurrentBag<double>();
            var edgeLenList = new ConcurrentBag<double>();
            ParallelLoopResult result = Parallel.ForEach<int>(
                   mesh.TriangleIndices(),
                   (tid) =>
                   {
                       if (GeometryUtils.IsDegenerativeTriangle(mesh, tid))
                           return;
                       var triangle = mesh.GetTriangle(tid);
                       var a = mesh.GetVertex(triangle.a);
                       var b = mesh.GetVertex(triangle.b);
                       var c = mesh.GetVertex(triangle.c);
                       var area = MathUtil.Area(a, b, c);
                       //находим длину ребра правильного треугольника с площадью area
                       var targetLen = Math.Sqrt(area * 4.0 / Math.Sqrt(3.0));
                       var triangleCurentAvgLen = ((a - b).Length + (a - c).Length + (c - b).Length) / 3.0;
                       //высчитываем коэф. соответствия текущего треугольнику правильному
                       var targetCoef = targetLen / triangleCurentAvgLen;
                       coefList.Add(targetCoef);
                       edgeLenList.Add(targetLen);
                   }
            );
            var angle = 3.0;
            var edgeCounter = 0;

            //отвечает за вычисление количеста рёбер, треугольники между которым формируют угол больше 3 градусов, т.е имеют изгибы
            //чем больше изгибов - тем меньше должна быть результирующая сетка
            ParallelLoopResult result2 = Parallel.ForEach<int>(
                   mesh.EdgeIndices(),
                   (eid) =>
                   {
                       double fAngle = MeshUtil.OpeningAngleD(mesh, eid);
                       if (fAngle > angle)
                       {
                           Interlocked.Increment(ref edgeCounter);
                       }
                   }
            );
            //соотножение граней с острым углом к общему числу граней
            //для сложным сеток с множеством изгибов edgeSharpCoef будет стремиться к 1
            //для простых (с плоскими гранями) к нулю
            var edgeSharpCoef = edgeCounter / (double)mesh.EdgeCount;

            //коэффициент edgeSharpCoef должен быть в диапозоне от 1 до 2,
            //но значимоcть нужно увеличить чувствительность к edgeSharpCoef, потому добавим множитель 3,0
            edgeSharpCoef = Math.Max(1.0, 2.0 - edgeSharpCoef * 3.0);

            //коэффициент усложнения модели
            //то есть насколько нужно уменьшить длину среднего ребра, чтобы 
            //модель не потеряла в деталях.
            //должна быть в пределах от 0 до 1
            //в сетках с преимущественно правильными треугольниками coefList.Average() будет стремится к единице, с вытянутыми к нулю
            var resultCoef = Math.Max(1.0, coefList.Average() * edgeSharpCoef);//ограничение снизу

            //средняя длина грани треугольника, если бы все треугольники были правильными
            var resultLen = edgeLenList.Average();

            //получение нового размера длины треугольника
            var newValue = resultCoef * resultLen;

            //scaleCoef отвечает за ограничение сверху
            //средняя длина ребра avg умноженная на этот коэффициент
            //увеличиывает ограничение сверху, что позволяет упрощать несложные модели.
            var scaleCoef = 1.3;

            //ограничение сверху
            newValue = Math.Min(newValue, avge * scaleCoef);

            return newValue;
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
            DeleteDegenerateTriangles(mesh);
            //Remesher r = new Remesher(mesh);

            //RemesherPro это расширения класса Remesher,
            //которая должна быть эфективнее с точки зрения производительности
            RemesherPro r = new RemesherPro(mesh);

            r.PreventNormalFlips = true;
            r.Precompute();
            r.AllowCollapseFixedVertsWithSameSetID = AllowCollapseFixedVertsWithSameSetID;
            r.EnableParallelProjection = UseParallelCalculations;
            r.EnableParallelSmooth = UseParallelCalculations;
            r.EnableSmoothInPlace = true;
            r.EdgeFlipTolerance = 0.01;
            r.SmoothType = SmoothType;
            r.ProjectionMode = TargetProjectionMode;


            MeshConstraints cons = new MeshConstraints();
            EdgeRefineFlags edgeRefineFlags = EdgeRefineFlags.NoFlip;
            _constraintVtxCounter = 0;
            VtxConstrainsAngle(r.Mesh, cons, Angle);
            if (Reprojection)
            {
                r.SetProjectionTarget(MeshProjectionTarget.Auto(r.Mesh));
                cancelToken.ThrowIfCancellationRequested();
            }

            var resultEdgeConstr = new List<int>();
            if (EnableFaceGroup)
            {
                var faceGroupEdges = GetEdgesIdConstrainsByGroups(mesh);
                resultEdgeConstr.AddRange(faceGroupEdges);
            }

            if (KeepAngle)
            {
                var angleEdges = GetEdgesIdConstrainsByAngle(mesh, Angle);
                resultEdgeConstr.AddRange(angleEdges);
            }
            if (resultEdgeConstr.Count > 0)
            {
                resultEdgeConstr = resultEdgeConstr.Distinct().ToList();
                MeshConstraints(mesh, resultEdgeConstr, edgeRefineFlags, cons);
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
            cancelToken.ThrowIfCancellationRequested();
            DeleteDegenerateTriangles(mesh);

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
            var vtxValid = new List<int>();

            ///Мы можем удалять точки на гранях только если она принадлежит линии или контуру
            ///то есть имеет только 2 родительских ребра ограничения. Если выбрать другие, например
            ///с 3 ребрами, то углы куба могут быть удалены, что ведёт изменение формы
            const int validParentEdgeCount = 2;

            foreach (var item in vtxsID)
            {
                if (item.Value.Count == validParentEdgeCount &&
                    !GeometryUtils.IsSharpEdges(mesh, item.Key, vtxsID, AngleEdge))
                {
                    vtxValid.Add(item.Key);
                }
                else
                {
                    ///выставление ограничение VertexConstraint работает так, что вершине можно присвоить индекс.
                    ///Настройка AllowCollapseFixedVertsWithSameSetID по возможности будет схлопывать грани с одинаковыми
                    ///номерами, значит выставить нужно так, что все углы должны быть помечаны уникальными номерами
                    ///таковым является counter
                    constraints.SetOrUpdateVertexConstraint(item.Key, new VertexConstraint(true, this._constraintVtxCounter));
                }
                ++this._constraintVtxCounter;
            }

            ///Что бы каждая отдельное ребро грани могло схлопывать точки и не перемешиваться с другими рёбрами
            ///нужно каждую пометить своим номером.
            while (vtxValid != null && vtxValid.Count > 0)
            {
                var firstVtxGroup = vtxValid.First();
                vtxValid.Remove(firstVtxGroup);

                var queue = new Queue<int>();
                queue.Enqueue(firstVtxGroup);
                constraints.SetOrUpdateVertexConstraint(firstVtxGroup, new VertexConstraint(true, _constraintVtxCounter));
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
                            constraints.SetOrUpdateVertexConstraint(vtx2, new VertexConstraint(true, _constraintVtxCounter));
                        }
                    }
                } while (queue.Count > 0);

                ++_constraintVtxCounter;
            }
        }

        public MeshConstraints GetMeshConstraints(DMesh3 mesh)
        {
            if (!mesh.CheckValidity(eFailMode: FailMode.ReturnOnly))
            {
                FixNormals(mesh);
            }
            var cons = new MeshConstraints();
            EdgeRefineFlags edgeRefineFlags = EdgeRefineFlags.NoFlip;
            VtxConstrainsAngle(mesh, cons, Angle);
            List<int> faceGroupEdges = null;
            List<int> angleEdges = null;
            var resultEdgeConstr = new List<int>();
            if (EnableFaceGroup)
            {
                faceGroupEdges = GetEdgesIdConstrainsByGroups(mesh);
                resultEdgeConstr.AddRange(faceGroupEdges);
            }

            if (KeepAngle)
            {
                angleEdges = GetEdgesIdConstrainsByAngle(mesh, Angle);
                resultEdgeConstr.AddRange(angleEdges);
            }
            if (resultEdgeConstr.Count > 0)
            {
                resultEdgeConstr = resultEdgeConstr.Distinct().ToList();
                MeshConstraints(mesh, resultEdgeConstr, edgeRefineFlags, cons);
            }

            ConstraintsDegenerateTri(mesh, cons);
            return cons;
        }
        private void ConstraintsDegenerateTri(DMesh3 mesh, MeshConstraints constraints)
        {
            var contspointFlag = 99901;
            foreach (int triangleid in mesh.TriangleIndices())
            {
                if (GeometryUtils.IsDegenerativeTriangle(mesh, triangleid))
                {
                    var triangle = mesh.GetTriangle(triangleid);
                    var pointId = GeometryUtils.GetDegenerateVtxId(mesh, triangleid);
                    constraints.SetOrUpdateVertexConstraint(pointId, new VertexConstraint(true, contspointFlag));
                }

            }
        }

        /// <summary>
        /// Вычисление сколько раз в списке граней встречается каждая из точек
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="edgesId"></param>
        /// <returns>словарь(id вертекса,кол-во упоминаний)></id></returns>
        private Dictionary<int, List<int>> GetVtxIDStats(DMesh3 mesh, List<int> edgesId)
        {
            var dict = new Dictionary<int, List<int>>();

            foreach (var eid in edgesId)
            {
                var edge = mesh.GetEdgeV(eid);
                for (int i = 0; i < 2; ++i)
                {
                    var value = edge[i];
                    if (!dict.ContainsKey(value))
                    {
                        dict.Add(value, new List<int>());
                    }
                    dict[value].Add(eid);
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
            if (mesh.VertexCount > 2 && mesh.TriangleCount > 0)
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

        /// <summary>
        /// Удаляет нулевые треугольники используя угол Angle
        /// </summary>
        /// <param name="mesh"></param>
        private void DeleteDegenerateTriangles(DMesh3 mesh)
        {
            GeometryUtils.DeleteDegenerateTriangle(mesh, Angle);
        }


        /// <summary>
        /// Устанавливает ограничение на острые углы сетки
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="constraints"></param>
        /// <param name="angle"></param>
        private void VtxConstrainsAngle(DMesh3 mesh, MeshConstraints constraints, double angle)
        {
            var sharpVtxIDs = new ConcurrentBag<int>();
            ParallelLoopResult result = Parallel.ForEach<int>(
                   mesh.VertexIndices(),
                   (vid) =>
                   {
                       if (GeometryUtils.OneRingVtxIsSharp(mesh, vid, angle))
                       {
                           sharpVtxIDs.Add(vid);
                       }
                   }
            );
            foreach (var vid in sharpVtxIDs)
            {
                constraints.SetOrUpdateVertexConstraint(vid, new VertexConstraint(true, _constraintVtxCounter));
                ++_constraintVtxCounter;
            }
        }
    }
}