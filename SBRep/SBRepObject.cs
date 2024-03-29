﻿using g3;
using SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using static SBRep.SBRepToMeshBuilderV2;

namespace SBRep
{
    public class SBRepObject
    {
        public IndexedCollection<SBRep_Vtx> Vertices { get; private set; }
        public IndexedCollection<SBRep_Edge> Edges { get; private set; }
        public IndexedCollection<SBRep_Verge> Verges { get; private set; }
        public IndexedCollection<SBRep_Loop> Loops { get; private set; }
        public IndexedCollection<SBRep_Face> Faces { get; private set; }

        public SBRepObject()
        {
            Vertices = new IndexedCollection<SBRep_Vtx>();
            Edges = new IndexedCollection<SBRep_Edge>();
            Verges = new IndexedCollection<SBRep_Verge>();
            Loops = new IndexedCollection<SBRep_Loop>();
            Faces = new IndexedCollection<SBRep_Face>();
        }

        public SBRepObject(SBRepObject other) : this()
        {
            Copy(other);
        }

        public void Copy(SBRepObject other)
        {
            Vertices.Clear();
            Edges.Clear();
            Verges.Clear();
            Loops.Clear();
            Faces.Clear();
            if (other == null)
                return;
            foreach (var v in other.Vertices)
            {
                Vertices.Add(new SBRep_Vtx(v));
            }
            foreach (var edge in other.Edges)
            {
                Edges.Add(new SBRep_Edge(edge));
            }
            foreach (var verge in other.Verges)
            {
                Verges.Add(new SBRep_Verge(verge));
            }
            foreach (var loop in other.Loops)
            {
                Loops.Add(new SBRep_Loop(loop));
            }
            foreach (var face in other.Faces)
            {
                Faces.Add(new SBRep_Face(face));
            }
        }
        public int FindVertex(Vector3d vertex)
        {
            foreach (var vtx in Vertices)
            {
                if (Geometry2DHelper.EqualVectors(vtx.Coordinate, vertex))
                    return vtx.ID;
            }
            return -1;
        }

        public int AddVertex(Vector3d vertex)
        {
            var existVertexId = FindVertex(vertex);
            if (existVertexId != -1)
                return existVertexId;
            var newVertex = new SBRep_Vtx()
            {
                Coordinate = vertex,
            };
            Vertices.Add(newVertex);
            return newVertex.ID;
        }

        public int AddEdge(int indexA, int indexB, int parentVergeID = -1)
        {
            if (!(Vertices.ContainsKey(indexA) && Vertices.ContainsKey(indexB))) return -1;
            var newEdge = new SBRep_Edge()
            {
                Vertices = new Index2i(indexA, indexB),
                Parent = parentVergeID
            };
            Edges.Add(newEdge);
            Vertices[indexA].Parents.Add(newEdge.ID);
            Vertices[indexB].Parents.Add(newEdge.ID);
            return newEdge.ID;
        }

        public int AddVerge(IEnumerable<int> edgesIDs)
        {
            if (!edgesIDs.All(eid => Edges.ContainsKey(eid))) return -1;
            //TODO проверка на то, все ли они соеденены между собой
            var newVerge = new SBRep_Verge()
            {
                Edges = new List<int>(edgesIDs),
            };
            Verges.Add(newVerge);
            foreach (var eid in edgesIDs)
            {
                Edges[eid].Parent = newVerge.ID;
            }
            return newVerge.ID;
        }
        private static bool LoopContains(IEnumerable<SBRep_Loop> loops, IEnumerable<int> edgeIDs, ref int id)
        {
            id = -1;

            foreach (var loop in loops)
            {
                if (loop.Verges.Count != edgeIDs.Count())
                    continue;
                var edgesA = loop.Verges;
                var edgesB = edgeIDs;
                var identical = edgesA.All(x => edgesB.Contains(x));
                if (identical)
                {
                    id = loop.ID;
                    return true;
                }
            }

            return false;
        }

        public int AddLoop(IEnumerable<int> vergesIds)
        {
            if (!vergesIds.All(vergeid => Verges.ContainsKey(vergeid))) return -1;

            var lid = -1;
            if (LoopContains(Loops, vergesIds, ref lid))
            {
                return lid;
            }

            var newLoop = new SBRep_Loop()
            {
                Verges = new List<int>(vergesIds),
            };
            Loops.Add(newLoop);
            foreach (var vergeid in vergesIds)
            {
                Verges[vergeid].Parents.Add(newLoop.ID);
            }
            return newLoop.ID;
        }

        public int AddFace(int groupID, PlaneFace plane, Vector3d normal, int outsideLoopId, IEnumerable<int> insideLoopsIds = null)
        {
            if (!Loops.ContainsKey(outsideLoopId)) return -1;
            if (insideLoopsIds != null)
            {
                if (!insideLoopsIds.All(lid => Loops.ContainsKey(lid))) return -1;
            }
            List<int> insideLoops = new List<int>();
            if (insideLoopsIds != null)
                insideLoops = new List<int>(insideLoopsIds);

            var newFace = new SBRep_Face()
            {
                GroupID = groupID,
                Plane = plane,
                //Normal = normal,
                OutsideLoop = outsideLoopId,
                InsideLoops = insideLoops
            };
            Faces.Add(newFace);
            Loops[outsideLoopId].Parents.Add(newFace.ID);
            if (insideLoops != null)
            {
                foreach (var lid in insideLoops)
                {
                    Loops[lid].Parents.Add(newFace.ID);
                }
            }
            return newFace.ID;
        }

        public void RemoveEdge(int eid)
        {
            var edge = Edges[eid];
            var indexA = edge.Vertices.a;
            var indexB = edge.Vertices.b;
            Vertices[indexA].Parents.Remove(edge.ID);
            Vertices[indexB].Parents.Remove(edge.ID);

            var parent = edge.Parent;
            var parentVerge = Verges[parent];
            parentVerge.Edges.Remove(edge.ID);
            //if (parentVerge.Edges.Count == 0)//TODO
            //RemoveVerge(parent);
            Edges.Remove(edge);
        }

        public void RemoveVerge(int vergeId)
        {
            var verge = Verges[vergeId];
            foreach (var eid in verge.Edges)
            {
                Edges[eid].Parent = -1;
            }
            foreach (var lid in verge.Parents)
            {
                Loops[lid].Verges.Remove(vergeId);
                //if (Loops[lid].Verges.Count == 0)//TODO
                //RemoveLoops(lid);
            }
            Verges.Remove(verge);
        }

        public void RemoveFace(int faceId)
        {
            var face = Faces[faceId];
            foreach (var insideLoop in face.InsideLoops)
            {
                Loops[insideLoop].Parents.Remove(faceId);
            }
            Loops[face.OutsideLoop].Parents.Remove(faceId);
            Faces.Remove(face);
        }

        /// <summary>
        /// Добавляет точки vertices в ребро с индексом eid
        /// </summary>
        /// <param name="eid">индекс грани</param>
        /// <param name="vertices">список точек с индексами</param>
        /// <returns>словарь (старый индекс вершины, новый индекс вершины)</returns>
        public Tuple<Dictionary<int, int>, IEnumerable<int>> AddPointsOnEdge(int eid, IEnumerable<IIndexedVector3d> vertices)
        {
            if (!Edges.ContainsKey(eid)) return null;
            var edge = Edges[eid];
            var parent = edge.Parent;
            var parentVerge = Verges[parent];

            var edges = new List<int>();
            var indexA = edge.Vertices.a;
            var indexB = edge.Vertices.b;
            var pointsIndexesDictionary = new Dictionary<int, int>();
            //добавляем в объект набор точек и получаем словарь с индексами
            foreach (var vtx in vertices)
            {
                var newIndex = AddVertex(vtx.Coordinate);
                pointsIndexesDictionary.Add(vtx.ID, newIndex);
                vtx.ID = newIndex;
            }

            //добавляем в Edges (подразумеваем, что точки приходят отсортированными от A до B)
            var previewsPointId = indexA;
            for (int i = 0; i < vertices.Count() + 1; ++i)
            {
                int currentPointId = -1;
                if (i == vertices.Count())
                    currentPointId = indexB;
                else
                    currentPointId = pointsIndexesDictionary.ElementAt(i).Value;
                var newEdgeId = AddEdge(previewsPointId, currentPointId, parent);
                edges.Add(newEdgeId);
                previewsPointId = currentPointId;
            }

            RemoveEdge(edge.ID);
            foreach (var addedEdge in edges)
            {
                parentVerge.Edges.Add(addedEdge);
            }
            return new Tuple<Dictionary<int, int>, IEnumerable<int>>(pointsIndexesDictionary, edges);
        }

        /// <summary>
        /// Разделяет у всех плоскостей петли на внешние и внутренние
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void DefineFacesOutsideLoop()
        {
            foreach (var face in Faces)
            {
                if (face.InsideLoops == null || face.InsideLoops.Count == 0)
                    throw new Exception("Отсутствие петель у поверхности");

                if (face.InsideLoops.Count == 1)
                {
                    face.OutsideLoop = face.InsideLoops.First();
                    face.InsideLoops.Clear();
                }
                else
                {
                    double maxArea = -1.0;
                    int maxIndex = -1;
                    foreach (var lid in face.InsideLoops)
                    {
                        var area = GetLoopArea(lid);
                        if (area > maxArea)
                        {
                            maxArea = area;
                            maxIndex = lid;
                        }
                    }
                    face.OutsideLoop = maxIndex;
                    face.InsideLoops.Remove(maxIndex);
                }
            }
        }

        /// <summary>
        /// Для всех примитивов индексирует кем они использованы (parent свойство)
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void RedefineFeedbacks()
        {
            foreach (var loop in Loops)
            {
                loop.Parents = new List<int>();
            }
            foreach (var loopPart in Verges)
            {
                loopPart.Parents = new List<int>();
            }
            foreach (var edge in Edges)
            {
                edge.Parent = -1;
            }
            foreach (var vtx in Vertices)
            {
                vtx.Parents = new List<int>();
            }

            foreach (var face in Faces)
            {
                if (face.OutsideLoop != -1)
                    Loops[face.OutsideLoop].Parents.Add(face.ID);
                foreach (var lid in face.InsideLoops)
                {
                    Loops[lid].Parents.Add(face.ID);
                }
            }

            foreach (var loop in Loops)
            {
                foreach (var leid in loop.Verges)
                {
                    Verges[leid].Parents.Add(loop.ID);
                }
            }

            foreach (var le in Verges)
            {
                foreach (var eid in le.Edges)
                {
                    if (Edges[eid].Parent != -1)
                        throw new Exception();
                    Edges[eid].Parent = le.ID;
                }
            }

            foreach (var edge in Edges)
            {
                Vertices[edge.Vertices.a].Parents.Add(edge.ID);
                Vertices[edge.Vertices.b].Parents.Add(edge.ID);
            }
        }

        #region Data Access
        public IEnumerable<SBRep_Face> GetFaces()
        {
            return Faces;
        }
        public IEnumerable<int> GetFacesIds()
        {
            return Faces.GetIndexes();
        }

        public SBRep_Face GetFace(int id)
        {
            return Faces[id];
        }

        public Vector3d GetVertex(int id)
        {
            return Vertices[id].Coordinate;
        }

        public IEnumerable<Vector3d> GetVertices()
        {
            return Vertices.Select(x => x.Coordinate);
        }
        public IEnumerable<int> GetVerticesIds()
        {
            return Vertices.GetIndexes();
        }

        public IEnumerable<int> GetEdgesIdFromLoopId(int lid)
        {
            if (!Loops.ContainsKey(lid))
                return null;
            return Loops[lid].Verges
                .SelectMany(x => Verges[x].Edges)
                .Distinct()
                .ToList();
        }

        public IEnumerable<int> GetEdgesFromFaceId(int faceId)
        {
            var loops = GetFacesLoops(faceId);
            var edgesIds = new List<int>();
            foreach (var lid in loops)
            {
                edgesIds.AddRange(GetEdgesIdFromLoopId(lid));
            }
            return edgesIds
                .Distinct()
                .ToList();
        }

        //public IEnumerable<int> GetEdgesIdFromLoopId(int lid)
        //{
        //    if (!Loops.ContainsKey(lid))
        //        return null;
        //    var vergesIds = GetEdgesIdFromLoopId(lid);
        //    if (vergesIds == null)
        //        return null;
        //    return vergesIds.SelectMany(vergeId => Verges[vergeId].Edges).ToList();
        //}

        public IEnumerable<int> GetVerticesFromEdgesIds(IEnumerable<int> eids)
        {
            return eids
                .SelectMany(eid => new List<int>() { Edges[eid].Vertices.a, Edges[eid].Vertices.b })
                .Distinct()
                .ToList();
        }

        public IEnumerable<int> GetEdgesFromFaces(IEnumerable<int> faces)
        {
            return faces.SelectMany(faceID => GetEdgesFromFaceId(faceID))
                .Distinct()
                .ToList();
        }

        public IEnumerable<Vector3d> GetCoordinates(IEnumerable<int> vtxIds)
        {
            return vtxIds.Select(vid => Vertices[vid].Coordinate).ToList();
        }
        public Dictionary<int, Vector3d> GetCoordinatesWithId(IEnumerable<int> vtxIds)
        {
            var dict = new Dictionary<int, Vector3d>();
            foreach (var vid in vtxIds)
            {
                dict.Add(vid, Vertices[vid].Coordinate);
            }
            return dict;
        }


        public IEnumerable<Index2i> GetEdgesVtxs(IEnumerable<int> edgeIds)
        {
            return edgeIds.Select(eid => Edges[eid].Vertices).ToList();
        }
        public Tuple<Vector3d, Vector3d> GetEdgeCoordinates(int eid)
        {
            if (!Edges.ContainsKey(eid)) return null;
            var edge = Edges[eid];
            var pA = edge.Vertices.a;
            var pB = edge.Vertices.b;
            return new Tuple<Vector3d, Vector3d>(Vertices[pA].Coordinate, Vertices[pB].Coordinate);
        }

        public IEnumerable<int> GetFacesLoops(int faceID)
        {
            var face = GetFace(faceID);
            var loops = new List<int>();
            loops.Add(face.OutsideLoop);
            loops.AddRange(face.InsideLoops);
            return loops;
        }

        public IEnumerable<int> GetFaceNeighborsIndexes(int faceID)
        {
            var face = GetFace(faceID);
            var neighbors = GetFacesLoops(faceID)
                .SelectMany(lid => Loops[lid].Verges)
                .Distinct()
                .SelectMany(vergeID => Verges[vergeID].Parents)
                .Distinct()
                .SelectMany(lid => Loops[lid].Parents)
                .Distinct()
                .ToList();
            neighbors.Remove(faceID);
            return neighbors;
        }
        #endregion

        /// <summary>
        /// Вернуть id вершины соседнюю к vid, соеденённых ребром eid
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="eid"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public int GetVertexNeigborIdByEdge(int vid, int eid)
        {
            if (!Edges.ContainsKey(eid))
                throw new Exception($"Нет ребра с ID {eid}");
            var edgePoints = Edges[eid].Vertices;
            if (edgePoints.a == vid || edgePoints.b == vid)
                return edgePoints.a == vid ? edgePoints.b : edgePoints.a;
            throw new Exception($"Ребро {eid} не содержит {vid}");
        }

        //private static void Show(SBRepObject obj, IEnumerable<int> edgesIDs)
        //{
        //    var keyValuePairs = new Dictionary<Color, IEnumerable<int>>();
        //    keyValuePairs.Add(Colors.Red, edgesIDs);

        //    SbrepVizualizer.ShowEdgePlot(obj, keyValuePairs);
           
        //}

        public static IEnumerable<IEnumerable<int>> BuildLoopsFromEdges(SBRepObject obj, IEnumerable<int> edgesIDs, bool recursiveFix = true)
        {
            if (edgesIDs.Count() < 3)
            {
                //Show(obj, edgesIDs);
                //throw new Exception("Недостаточно граней для графа");
                return new List<IEnumerable<int>>();
            }
            //проверяем критерий обходимости
            var edges = edgesIDs.Select(eid => obj.Edges[eid]).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.Vertices.a, edge.Vertices.b }
                )
                .Distinct()
                .ToList();

            var vertices = verticesIds.Select(vid => obj.Vertices[vid]).ToList();
            var vertParentsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var vtx in vertices)
            {
                var parents = vtx.Parents.Intersect(edgesIDs).ToList();
                if (parents.Count() % 2 == 1)
                {
                    //Show(obj, edgesIDs);
                    throw new Exception("Невозможно обойти граф");
                }
                vertParentsDict.Add(vtx.ID, parents);
            }
            var bypassEdges = new List<int>(edgesIDs);
            var loops = new List<IEnumerable<int>>();

            var edgeQueue = new Queue<int>();
            List<int> currentLoopEdges = null;
            var currentEdge = -1;
            SBRep_Edge current = null;
            while (bypassEdges.Count > 0 ||
                edgeQueue.Count > 0)
            {
                if (edgeQueue.Count == 0)
                {
                    currentLoopEdges = new List<int>();
                    loops.Add(currentLoopEdges);
                    currentEdge = bypassEdges.First();
                    currentLoopEdges.Add(currentEdge);
                    bypassEdges.Remove(currentEdge);
                }
                else
                    currentEdge = edgeQueue.Dequeue();
                var edgesNeighbors = new List<int>();
                current = obj.Edges[currentEdge];
                var vtxANeighbor = vertParentsDict[current.Vertices.a];
                var vtxBNeighbor = vertParentsDict[current.Vertices.b];

                if (vtxANeighbor.Count() <= 2)
                    edgesNeighbors.AddRange(vtxANeighbor);
                if (vtxBNeighbor.Count() <= 2)
                    edgesNeighbors.AddRange(vtxBNeighbor);
                edgesNeighbors = edgesNeighbors
                    .Where(x => bypassEdges.Contains(x))
                    .Distinct()
                    .ToList();
                foreach (var item in edgesNeighbors)
                {
                    currentLoopEdges.Add(item);
                    bypassEdges.Remove(item);
                    edgeQueue.Enqueue(item);
                }
            }

            if (recursiveFix)
            {
                var unLoops = loops.Where(loop => !IsLoopEdges(obj, loop)).ToList();

                var correctLoops = loops.Where(loop => IsLoopEdges(obj, loop)).ToList();
                loops.Clear();
                loops.AddRange(correctLoops);
                while (unLoops.Count() > 0)
                {
                    var unLoopsEdges = unLoops.SelectMany(loop => loop).ToList();
                    var tryFixLoops = BuildLoopsFromEdges(obj, unLoopsEdges, false);
                    var unLoopsAfterFix = tryFixLoops.Where(loop => !IsLoopEdges(obj, loop)).ToList();
                    var correctLoopsAfterFix = tryFixLoops.Where(loop => IsLoopEdges(obj, loop)).ToList();
                    loops.AddRange(correctLoopsAfterFix);
                    if (unLoopsAfterFix.Count() == unLoops.Count)
                        break;
                    unLoops = unLoopsAfterFix;
                }
            }
            loops = loops.Where(x => x.Count() > 0).ToList();
            return loops;
        }

        public static bool IsLoopEdges(SBRepObject obj, IEnumerable<int> edgesIds)
        {
            if(edgesIds.Count() == 0)
                return false;
            var edges = edgesIds.Select(eid => obj.Edges[eid]).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.Vertices.a, edge.Vertices.b }
                )
                .Distinct()
                .ToList();
            var vertices = verticesIds.Select(vid => obj.Vertices[vid]).ToList();
            foreach (var vtx in vertices)
            {
                var parents = vtx.Parents.Intersect(edgesIds).ToList();
                if (parents.Count() < 2)
                    return false;
                if (parents.Count() % 2 == 1)
                    return false;
                    //throw new Exception("Петля содержит развилки");
            }
            return true;
        }

        public static IEnumerable<IEnumerable<int>> BuildLoopsFromEdgesByAngle(SBRepObject obj, Dictionary<int, bool> edgesIDs)
        {
            //проверяем критерий обходимости
            var edges = edgesIDs.Select(eid => obj.Edges[eid.Key]).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.Vertices.a, edge.Vertices.b }
                )
                .Distinct()
                .ToList();
            var vertices = verticesIds.Select(vid => obj.Vertices[vid]).ToList();
            var vertParentsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var vtx in vertices)
            {
                var parents = vtx.Parents.Intersect(edgesIDs.Keys);
                if (parents.Count() % 2 == 1)
                    throw new Exception("Невозможно обойти граф");
                vertParentsDict.Add(vtx.ID, parents);
            }
            var bypassEdges = new List<int>(edgesIDs.Keys);
            var loops = new List<IEnumerable<int>>();
            //var verticesQueue = new Queue<int>();
            int loopBeginVid = -1;
            int currentVid = -1;
            int nextVid = -1;
            int lastEid = -1;
            List<int> currentLoopEdges = null;
            while (bypassEdges.Count != 0)
            {
                if (nextVid == -1)
                {
                    currentLoopEdges = new List<int>();
                    loops.Add(currentLoopEdges);
                    loopBeginVid = verticesIds.Where(
                        vid => vertParentsDict[vid]
                        .Where(x => bypassEdges.Contains(x))
                        .Count() <= 2).First();

                    verticesIds.Remove(loopBeginVid);
                    currentVid = loopBeginVid;
                }
                else
                    currentVid = nextVid;
                var parents = vertParentsDict[currentVid]
                    .Where(x => bypassEdges.Contains(x))
                    .ToList();
                if (parents.Count() > 2)
                {
                    var currentVtxCoord = obj.Vertices[currentVid].Coordinate;
                    var currentEdgeSecondVtxId = obj.GetVertexNeigborIdByEdge(currentVid, lastEid);
                    var mainEdgeVtxCoord = obj.Vertices[currentEdgeSecondVtxId].Coordinate;
                    var lastVector = mainEdgeVtxCoord - currentVtxCoord;
                    var parentsEid = parents.Where(x => edgesIDs[x] != edgesIDs[lastEid]).ToList();

                    if (parentsEid.Count == 0)
                    {
                        parentsEid = parents;
                    }
                    Debug.Assert(parentsEid.Count() > 0);

                    var parentsNextPoints = new Dictionary<int, Vector3d>();
                    foreach (var eid in parentsEid)
                    {
                        var curentEdgeSecondCoord = obj.Vertices[obj.GetVertexNeigborIdByEdge(currentVid, eid)].Coordinate;
                        parentsNextPoints.Add(eid, curentEdgeSecondCoord - currentVtxCoord);
                    }
                    var minAngle = double.MaxValue;
                    var minEid = -1;

                    var parentsNoCross = vertParentsDict[currentVid]
                        .Where(x => edgesIDs[x] == edgesIDs[lastEid] && x != lastEid)
                        .Select(eid =>
                        {
                            var points = obj.GetEdgeCoordinates(eid);
                            return (points.Item2 - points.Item1).xy;
                        }).ToList();


                    foreach (var eidVector in parentsNextPoints)
                    {
                        var eVector = eidVector.Value;
                        var dot = eVector.Dot(lastVector) / (lastVector.Length * eVector.Length);
                        var angle = Math.Acos(MathUtil.Clamp(dot, -1.0, 1.0)) * (180.0 / Math.PI);
                        if (minAngle > angle)
                        {
                            //проверка, что мы подобным переходом не пересекаем грани, которую по другую сторону
                            var result = parentsNoCross
                                .All(edge => Geometry2DHelper.EdgesInterposition(Vector2d.Zero, edge, lastVector.xy, eVector.xy, 1e-6).Intersection == IntersectionVariants.NoIntersection);
                            if (result)
                            {
                                minAngle = angle;
                                minEid = eidVector.Key;
                            }
                        }
                    }
                    Debug.Assert(minEid != -1);
                    Debug.Assert(minAngle != double.MaxValue);
                    parents = new List<int>() { minEid };
                }
                if (parents.Count() <= 2)
                {
                    var parent = parents.First();
                    nextVid = obj.GetVertexNeigborIdByEdge(currentVid, parent);
                    currentLoopEdges.Add(parent);
                    bypassEdges.Remove(parent);
                    lastEid = parent;
                }
                if (nextVid == loopBeginVid)
                    nextVid = -1;
            }
            //loops = loops.Where(x=> x.Count() > 0).ToList();
            return loops;
        }

        public void RebuildVerges()
        {
            //TODO
        }

        /// <summary>
        /// Получить замкнутый контур из петли под индексом lid
        /// </summary>
        /// <param name="lid">индекс петли</param>
        /// <returns>упорядоченный список координат</returns>
        public IEnumerable<Vector3d> GetClosedContour(int lid)
        {

            if (!Loops.ContainsKey(lid))
                return null;
            return GetClosedContourVtx(lid).Select(x => x.Coordinate).ToList();
        }

        /// <summary>
        /// Получить замкнутый контур из петли под индексом lid
        /// </summary>
        /// <param name="lid">индекс петли</param>
        /// <returns>упорядоченный список Объектов вершин</returns>
        public IEnumerable<SBRep_Vtx> GetClosedContourVtx(int lid)
        {
            if (!Loops.ContainsKey(lid))
                return null;

            var edgesIDs = GetEdgesIdFromLoopId(lid);
            int startEdge = edgesIDs.First();
            int currentEdge = -1;
            var lastVtxID = -1;
            var vertexOrderList = new List<SBRep_Vtx>();
            while (startEdge != currentEdge)
            {
                if (currentEdge == -1)
                    currentEdge = startEdge;

                var edge = Edges[currentEdge];
                //edgesIDs.RemoveAt(currentEdge);
                var verticeIndexes = edge.Vertices;
                int vtxID = -1;
                if (lastVtxID == -1)
                {
                    vtxID = verticeIndexes.a;
                }
                else
                {
                    vtxID = verticeIndexes.a == lastVtxID ? verticeIndexes.b : verticeIndexes.a;
                }
                var vtx = Vertices[vtxID];
                vertexOrderList.Add(vtx);
                lastVtxID = vtx.ID;

                var nextEdge = vtx.Parents.Where(eid => eid != currentEdge && edgesIDs.Contains(eid)).ToList();

                if (nextEdge.Count == 0)
                    throw new Exception("Нет пути");
                //if (nextEdge.Count != 1)
                //    throw new Exception("У петли почему то появилась развилка");
                currentEdge = nextEdge.First();
            }

            return vertexOrderList;
        }
        /// <summary>
        /// Получить замкнутый контур из петли под индексом lid
        /// </summary>
        /// <param name="lid">индекс петли</param>
        /// <returns>упорядоченный список Объектов вершин</returns>
        public IEnumerable<SBRep_Edge> GetClosedContourEdgesFromLoopID(int lid)
        {
            if (!Loops.ContainsKey(lid))
                return null;

            var edgesIDs = GetEdgesIdFromLoopId(lid);
            int startEdge = edgesIDs.First();
            int currentEdge = -1;
            var lastVtxID = -1;
            var edgesOrderList = new List<SBRep_Edge>();
            while (startEdge != currentEdge)
            {
                if (currentEdge == -1)
                {
                    currentEdge = startEdge;
                }

                var edge = Edges[currentEdge];
                //edgesIDs.RemoveAt(currentEdge);
                var verticeIndexes = edge.Vertices;
                int vtxID = -1;
                if (lastVtxID == -1)
                {
                    vtxID = verticeIndexes.a;
                }
                else
                {
                    vtxID = verticeIndexes.a == lastVtxID ? verticeIndexes.b : verticeIndexes.a;
                }
                var vtx = Vertices[vtxID];
                //vertexOrderList.Add(vtx);
                lastVtxID = vtx.ID;

                var nextEdge = vtx.Parents.Where(eid => eid != currentEdge && edgesIDs.Contains(eid)).ToList();
                if (nextEdge.Count != 1)
                    throw new Exception("У петли почему то появилась развилка");
                currentEdge = nextEdge.First();
                edgesOrderList.Add(Edges[currentEdge]);
            }

            return edgesOrderList;
        }

        /// <summary>
        /// Площадь внутри петли
        /// </summary>
        /// <param name="lid">Индекс петли</param>
        /// <returns></returns>
        public double GetLoopArea(int lid)
        {
            var contour = GetClosedContour(lid);
            var loop = Loops[lid];
            var parentid = loop.Parents.FirstOrDefault();
            var face = Faces[parentid];
            var contour2d = ConvertPlaneContourTo2D(contour, face.Normal);
            return Geometry2DHelper.GetArea(contour2d);
        }

        public double GetFaceArea(int fid)
        {
            var face = Faces[fid];
            var area = GetLoopArea(face.OutsideLoop);
            foreach (var lid in face.InsideLoops)
            {
                area -= GetLoopArea(lid);
            }
            return area;
        }

        /// <summary>
        /// Вычисляет двухмерные координаты трёхмерного контура
        /// </summary>
        /// <param name="contour">плоский контур с 3д координатами</param>
        /// <param name="normal">нормаль поверхности, на которой находится контур</param>
        /// <returns></returns>
        public List<Vector2d> ConvertPlaneContourTo2D(IEnumerable<Vector3d> contour, Vector3d normal)
        {

            var transformMatrix = Geometry2DHelper.CalculateTransform(contour.First(), normal);

            var mtx = transformMatrix.Item1;
            var offset = transformMatrix.Item3;

            var points2d = new List<Vector2d>();
            foreach (var vertice in contour)
            {
                //смещение в 0
                var point = new Vector3d(vertice.x - offset.x, vertice.y - offset.y, vertice.z - offset.z);
                //применение матрицы преобразования в плоскость XOY
                var point2D = mtx * point;
                //Добавление в список двухмерных точек
                points2d.Add(new Vector2d(point2D.x, point2D.y));
            }
            return points2d;
        }

        public Dictionary<int, Vector2d> GetPointsFromVtxOnPlane(IEnumerable<int> vtxIds, PlaneFace plane)
        {
            var firstVtx = Vertices[vtxIds.First()];
            var transforms = Geometry2DHelper.CalculateTransform(firstVtx.Coordinate, plane.Normal);
            var mtx = transforms.Item1;
            var offset = transforms.Item3;
            var result = new Dictionary<int, Vector2d>();
            foreach (var vid in vtxIds)
            {
                var vertice = Vertices[vid];
                //смещение в 0
                var point = new Vector3d(vertice.Coordinate.x - offset.x, vertice.Coordinate.y - offset.y, vertice.Coordinate.z - offset.z);
                //применение матрицы преобразования в плоскость XOY
                var point2D = mtx * point;
                result.Add(vid, new Vector2d(point2D.x, point2D.y));
            }
            return result;
        }

        public void CalcBoundingBox(ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            minX = double.MaxValue; 
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;

            foreach (var vtx in Vertices)
            {
                var coord = vtx.Coordinate;
                if (coord.x > maxX)
                    maxX = coord.x;
                if (coord.x < minX)
                    minX = coord.x;
                if (coord.y > maxY)
                    maxY = coord.y;
                if (coord.y < minY)
                    minY = coord.y;
            }
        }

        public bool AreEquivalent(SBRepObject other, double eps = 1e-2, bool groupIDCheck = true)
        {
            if (Faces.Count != other.Faces.Count) return false;
            if (Loops.Count != other.Loops.Count) return false;
            if (Verges.Count != other.Verges.Count) return false;


            foreach (var face in Faces)
            {
                IEnumerable<SBRep_Face> otherFace = other.Faces;
                try
                {
                    if (groupIDCheck)
                        otherFace = otherFace
                            .Where(x => x.GroupID == face.GroupID).ToList();

                    otherFace = otherFace.Where(x => Geometry2DHelper.EqualVectors(x.Normal, face.Normal, eps)).ToList();

                    var currentFaceArea = GetFaceArea(face.ID);
                    otherFace = otherFace.Where(x => Math.Abs(other.GetFaceArea(x.ID) - currentFaceArea) < eps).ToList();

                    var currentFaceCenterPoint = Geometry2DHelper.GetWeightedCenter(GetClosedContour(face.OutsideLoop));
                    otherFace = otherFace.Where(x => Geometry2DHelper.EqualVectors(
                            currentFaceCenterPoint,
                            Geometry2DHelper.GetWeightedCenter(other.GetClosedContour(x.OutsideLoop)), eps))
                        .ToList();
                }
                catch (Exception ex)
                {
                    return false;
                }


                if (otherFace == null || otherFace.Count() < 1)
                    return false;
            }

            return true;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (var face in Faces)
            {
                builder.AppendLine(face.ToString());
            }
            foreach (var item in Loops)
            {
                builder.AppendLine(item.ToString());
            }
            foreach (var item in Verges)
            {
                builder.AppendLine(item.ToString());
            }
            foreach (var item in Edges)
            {
                builder.AppendLine(item.ToString());
            }
            foreach (var item in Vertices)
            {
                builder.AppendLine(item.ToString());
            }
            return builder.ToString();
        }
    }
}
