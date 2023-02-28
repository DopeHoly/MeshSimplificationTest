﻿using CrossModulesData;
using g3;
using SurfaceInterpolation.Tools.SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace SurfaceInterpolation.Tools.SBRep
{

    public static class SBRepOperationsExtension
    {

        private static IntersectContour GetContourFromLoop(SBRepObject sbrep, int lid)
        {
            var loopVertices = sbrep.GetClosedContourVtx(lid);
            var loopEdges = sbrep.GetClosedContourEdgesFromLoopID(lid);
            return IntersectContour.FromSBRepLoop(loopVertices, loopEdges);
        }


        public static SBRepObject ContourProjection(this SBRepObject sbrep, List<Vector2d> contour, bool topDirection)
        {
            var obj = new SBRepObject(sbrep);
            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            int maxGroidID = obj.Faces.Select(face => face.GroupID).Max();
            var replaceIntdex = ++maxGroidID;
            foreach (var face in obj.Faces)
            {
                face.GroupID = face.GroupID != -1 ? face.GroupID : replaceIntdex;
            }

            maxGroidID = obj.Faces.Select(face => face.GroupID).Max();
            var groupIDsDict = new Dictionary<int, int>();

            var filteredFaces = obj.Faces.Where(face => comparer(face.Normal.z)).ToList();
            if (filteredFaces.Count == 0)
                return obj;
            //TODO тут нужно разделить конутр на N контуров без самопересечений, т.к. в исходном контуре они могут быть
            var projectionContour = IntersectContour.FromPoints(contour);
            var count = 0;
            foreach (var face in filteredFaces)
            {
                var projContour = new IntersectContour(projectionContour);
                var outsideLoop = new IntersectContour(GetContourFromLoop(obj, face.OutsideLoop));
                var insideLoops = face.InsideLoops.Select(lid =>
                 new IntersectContour(GetContourFromLoop(obj, lid)))
                    .ToList();
                var resultIntersect = IntersectContour.Intersect(projContour, outsideLoop);
                foreach (var insideLoop in insideLoops)
                {
                    resultIntersect = IntersectContour.Difference(resultIntersect, insideLoop);
                }
                ApplyIntersectContourToFace(obj, face.ID, resultIntersect, groupIDsDict, ref maxGroidID);
                ++count;
            }

            return obj;
        }

        private static int GetNewGroupIDFromDictionary(int id, Dictionary<int, int> groupIDsDict, ref int lastMaxID)
        {
            if (!groupIDsDict.ContainsKey(id))
            {
                lastMaxID += 2;
                groupIDsDict.Add(id, lastMaxID);
            }
            return groupIDsDict[id];
        }

        public static void ApplyIntersectContourToFace(SBRepObject sbrep, int faceID, IntersectContour contour, Dictionary<int, int> groupIDsDict, ref int maxGroidID)
        {
            var face = sbrep.Faces[faceID];
            //Обработка случаев, когда полностью грань попадает в проекцию или не попадает
            if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.OutPlane))
            {
                var faceEdgesPos = GetEdgesInsideOutside(sbrep, faceID, contour);
                if (faceEdgesPos.All(ePos => ePos.Value == true))
                    face.GroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                return;
            }

            //Обработка случая, когда попали проекцией полностью на контур, и ничего не задели
            //если все грани внутри, но больше 1 точки касаются грани, то скипаем, иначе юзаем упрощённый алгоритм
            if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane) &&
                contour.Points.Where(
                    point => point.Position.Mode == PointPositionMode.OnEdge ||
                    point.Position.Mode == PointPositionMode.OnVertex)
                .Count() < 2)
            {
                AddContourOnFace(sbrep, faceID, contour, groupIDsDict, ref maxGroidID);
                return;
            }

            //если дошёл до этого места, то контур пересекается либо с внешней петлёй, либо со внутренними

            //сначала добавляем все точки из contour в sbrep
            //var objString = sbrep.ToString();

            var pointIndexDict = new Dictionary<int, int>();

            //индексируем существующие точки
            IndexedExistingPoints(sbrep, contour, ref pointIndexDict);

            //добавляем точки на плоскость
            AddPlanePoint(sbrep, faceID, contour, ref pointIndexDict);

            //добавляем точки на грани
            var edgesReplaceDict = AddPointsToEdges(sbrep, faceID, contour, ref pointIndexDict);

            //добавляем новые рёбра
            var addedEdges = AddNewEdges(sbrep, contour, pointIndexDict, edgesReplaceDict);

            //вычисляем, какие рёбра внутри, какие снаружи
            var faceEdgesPosition = GetEdgesInsideOutside(sbrep, faceID, contour);
            //вычисляем множества рёбер для новых и старых граней
            var newFaceEdges = new List<int>();
            newFaceEdges.AddRange(
                faceEdgesPosition
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key));
            newFaceEdges.AddRange(addedEdges.Keys.ToList());

            var oldFacesEdges = new List<int>();
            oldFacesEdges.AddRange(
                faceEdgesPosition
                .Where(idPos => idPos.Value == false)
                .Select(idPos => idPos.Key));
            oldFacesEdges.AddRange(
                addedEdges
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key));


            //Собираем петли для новых граней
            var newFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, newFaceEdges);
            //Собираем петли для старых граней
            var oldFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, oldFacesEdges);

            //objString = sbrep.ToString();
            //Тут вычисляем, какие части петли нужно разделять на N частей
            //и составляем словарь какое ребро какой части петли соответствует


            //удаляем уже не нужные петли грани

            //Добавляем новые петли

            //sbrep.RebuildVerges();

            //пересобираем грани

            //применяем изменения к sbrep

            /////////////////////Зона эксперементов

            //тут получаем все рассматриваемые рёбра
            var edges = new List<int>();
            edges.AddRange(faceEdgesPosition.Keys.ToList());
            edges.AddRange(addedEdges.Keys.ToList());
            edges = edges.Distinct().ToList();

            //дальше собираем добавляем ломанные в brep по принципу - одно ребро - одна ломанная
            var vergeEdgeDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var eid in edges)
            {
                var id = sbrep.Edges[eid].Parent;
                if (id == -1)
                    continue;
                vergeEdgeDict.Add(eid, sbrep.Verges[id].Parents);
            }

            //дальше сносим все ломанные, которые к ним относятся
            var edgeToOldVergesID = new Dictionary<int, int>();
            var vergesIds = edges.Select(eid => sbrep.Edges[eid].Parent).Distinct().ToList();
            foreach (var vergeId in vergesIds)
            {
                if (vergeId == -1)
                    continue;
                var verge = sbrep.Verges[vergeId];
                foreach (var eid in verge.Edges)
                {
                    edgeToOldVergesID.Add(eid, vergeId);
                }
                sbrep.RemoveVerge(vergeId);
            }

            var dictEdgeNewVerge = new Dictionary<int, int>();
            foreach (var eid in edges)
            {
                var vergeIndex = sbrep.AddVerge(new List<int> { eid });
                dictEdgeNewVerge.Add(eid, vergeIndex);
                var verge = sbrep.Verges[vergeIndex];
                if (vergeEdgeDict.ContainsKey(eid))
                {
                    verge.Parents = new List<int>(vergeEdgeDict[eid]);
                    foreach (var loopId in vergeEdgeDict[eid])
                    {
                        sbrep.Loops[loopId].Verges.Add(vergeIndex);
                    }
                }
            }

            //Создаём петли и запихиваем их в sbrep
            var loopsByNewFaces = new IndexedCollection<SBRep_Loop>();
            foreach (var loop in newFacesLoops)
            {
                if (loop.Count() == 0) continue;
                var loopId = sbrep.AddLoop(loop.Select(eid => dictEdgeNewVerge[eid]).ToList());
                loopsByNewFaces.Add(sbrep.Loops[loopId]);
            }
            var loopsByOldFaces = new IndexedCollection<SBRep_Loop>();
            foreach (var loop in oldFacesLoops)
            {
                if (loop.Count() == 0) continue;
                var loopId = sbrep.AddLoop(loop.Select(eid => dictEdgeNewVerge[eid]).ToList());
                loopsByOldFaces.Add(sbrep.Loops[loopId]);
            }


            //дальше создаём по петлям все заготовки данных для грани (какие петли внутренние, какие внешние)

            var blackList = new List<int>();
            var facesLoopsColectionOld = new Dictionary<int, IEnumerable<int>>();
            foreach (var loop in loopsByOldFaces)
            {
                IEnumerable<int> insideLoopsIds = new List<int>();
                var checkedLoops = loopsByOldFaces.Where(checkedLoop => checkedLoop != loop).ToList();
                insideLoopsIds = GetInsideLoops(sbrep, loop, checkedLoops);
                blackList.AddRange(insideLoopsIds);
                facesLoopsColectionOld.Add(loop.ID, insideLoopsIds);
            }
            var facesLoopsColectionNew = new Dictionary<int, List<int>>();
            foreach (var loop in loopsByNewFaces)
            {
                facesLoopsColectionNew.Add(loop.ID, new List<int>());
            }

            //сносим старую грань
            sbrep.RemoveFace(faceID);

            //добавляем новые грани
            foreach (var facedLoops in facesLoopsColectionOld)
            {
                if (blackList.Contains(facedLoops.Key))
                    continue;
                sbrep.AddFace(face.GroupID, face.Plane, face.Normal, facedLoops.Key, facedLoops.Value);
            }
            ++maxGroidID;
            foreach (var facedLoops in facesLoopsColectionNew)
            {
                var newGroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                sbrep.AddFace(newGroupID, face.Plane, face.Normal, facedLoops.Key, facedLoops.Value);
            }

            //пересобираем verges по принадлежности одинаковым граням и по зонам связанности
            sbrep.RebuildVerges();//TODO а надо ли
            ////////////////////

        }

        public static IEnumerable<int> GetInsideLoops(SBRepObject sbrep, SBRep_Loop mainLoop, IEnumerable<SBRep_Loop> checkedLoop)
        {
            var resultCollection = new List<int>();
            var contour = new IntersectContour(GetContourFromLoop(sbrep, mainLoop.ID));
            foreach (var loop in checkedLoop)
            {
                if (LoopInsideContour(contour, sbrep, loop))
                    resultCollection.Add(loop.ID);
            }
            return resultCollection;
        }

        public static bool LoopInsideContour(IntersectContour contour, SBRepObject sbrep, SBRep_Loop loop)
        {
            var edgesIDs = sbrep.GetEdgesIdFromLoopId(loop.ID);
            foreach (var eid in edgesIDs)
            {
                var coords = sbrep.GetEdgeCoordinates(eid);
                var result = contour.EdgeInsideDeep(coords.Item1.xy, coords.Item2.xy);
                if (result != true)
                    return false;
            }
            return true;
        }

        public static void AddContourOnFace(SBRepObject sbrep, int faceID, IntersectContour contour, Dictionary<int, int> groupIDsDict, ref int maxGroidID)
        {
            var face = sbrep.Faces[faceID];
            var pointIndexDict = new Dictionary<int, int>();
            IndexedExistingPoints(sbrep, contour, ref pointIndexDict);
            //добавляем точки на плоскость
            AddPlanePoint(sbrep, faceID, contour, ref pointIndexDict);
            var edgesReplaceDict = AddPointsToEdges(sbrep, faceID, contour, ref pointIndexDict);
            //добавляем новые рёбра
            var addedEdges = AddNewEdges(sbrep, contour, pointIndexDict, edgesReplaceDict);
            //вычисляем множества рёбер для новых граней
            var newFaceEdges = new List<int>();
            newFaceEdges.AddRange(addedEdges.Keys.ToList());
            var newFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, newFaceEdges);
            Debug.Assert(newFacesLoops.Count() == 1);
            var vergeIndex = sbrep.AddVerge(newFacesLoops.First());
            var loopId = sbrep.AddLoop(new List<int> { vergeIndex });

            var newGroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);

            sbrep.AddFace(newGroupID, face.Plane, face.Normal, loopId);
            face.InsideLoops.Add(loopId);
        }

        public static Dictionary<int, bool?> GetEdgesInsideOutside(SBRepObject sbrep, int faceID, IntersectContour contour)
        {
            var facesEdges = sbrep.GetEdgesFromFaceId(faceID);
            return GetEdgesInsideOutside(sbrep, facesEdges, contour); ;
        }

        public static Dictionary<int, bool?> GetOutsideLoopEdgeInsideOutside(SBRepObject sbrep, int faceID, IntersectContour contour)
        {
            var facesEdges = sbrep.GetEdgesIdFromLoopId(sbrep.Faces[faceID].OutsideLoop);
            return GetEdgesInsideOutside(sbrep, facesEdges, contour);
        }

        public static Dictionary<int, bool?> GetEdgesInsideOutside(SBRepObject sbrep, IEnumerable<int> edgesIDs, IntersectContour contour)
        {
            var faceEdgesPosition = new Dictionary<int, bool?>();
            foreach (var edge in edgesIDs)
            {
                var points = sbrep.GetEdgePoints(edge);
                var a = points.Item1.xy;
                var b = points.Item2.xy;
                faceEdgesPosition.Add(edge, contour.EdgeInside(a, b));
            }
            return faceEdgesPosition;
        }

        public static void IndexedExistingPoints(
            SBRepObject sbrep,
            IntersectContour contour,
            ref Dictionary<int, int> pointIndexDict)
        {
            var pointsExisting = contour.Points.Where(point => point.Position.Mode == PointPositionMode.OnVertex);
            foreach (var point in pointsExisting)
            {
                pointIndexDict.Add(point.ID, point.Position.VtxID);
            }
        }

        public static void AddPlanePoint(
            SBRepObject sbrep,
            int faceID,
            IntersectContour contour,
            ref Dictionary<int, int> pointIndexDict)
        {
            var face = sbrep.Faces[faceID];
            var plane = face.Plane;
            var pointsInPlane = contour.Points.Where(point => point.Position.Mode == PointPositionMode.InPlane);

            foreach (var point in pointsInPlane)
            {
                var point3D = new Vector3d(
                    point.Coord.x,
                    point.Coord.y,
                    plane.GetZ(point.Coord.x, point.Coord.y));

                int newIndex = sbrep.AddVertex(point3D);
                pointIndexDict.Add(point.ID, newIndex);
            }
        }

        public static Dictionary<int, IEnumerable<int>> AddPointsToEdges(
            SBRepObject sbrep,
            int faceID,
            IntersectContour contour,
            ref Dictionary<int, int> pointIndexDict)
        {
            var face = sbrep.Faces[faceID];
            var plane = face.Plane;
            var pointsOnEdge = contour.Points.Where(point => point.Position.Mode == PointPositionMode.OnEdge);
            var pointsOnEdgeDict = new Dictionary<int, ICollection<Utils.Point>>();
            var pointsOnEdgeDictSorted = new Dictionary<int, ICollection<Utils.Point>>();
            //группируем по принадлежности граням
            foreach (var point in pointsOnEdge)
            {
                var eid = point.Position.EdgeID;
                if (!pointsOnEdgeDict.ContainsKey(eid))
                    pointsOnEdgeDict.Add(eid, new List<Utils.Point>());
                pointsOnEdgeDict[eid].Add(point);
            }
            //отсортировать по каждому ребру от точки А до B
            foreach (var points in pointsOnEdgeDict)
            {
                var eid = points.Key;
                var aIndex = sbrep.Edges[eid].Vertices.a;
                var bIndex = sbrep.Edges[eid].Vertices.b;
                var a = sbrep.Vertices[aIndex].Coordinate.xy;
                var b = sbrep.Vertices[bIndex].Coordinate.xy;
                pointsOnEdgeDictSorted.Add(eid, SortPointsOnEdge(a, b, points.Value));
            }
            var edgesDict = new Dictionary<int, IEnumerable<int>>();
            //добавить гряням точки
            foreach (var points in pointsOnEdgeDictSorted)
            {
                var points3DOnEdge = points.Value.Select(point =>
                    new SBRep_Vtx()
                    {
                        Coordinate = new Vector3d(
                            point.Coord.x,
                            point.Coord.y,
                            plane.GetZ(point.Coord.x, point.Coord.y)),
                        ID = point.ID
                    });
                var pointEdgesDict = sbrep.AddPointsOnEdge(points.Key, points3DOnEdge);
                edgesDict.Add(points.Key, pointEdgesDict.Item2);
                foreach (var pointIndexed in pointEdgesDict.Item1)
                {
                    pointIndexDict.Add(pointIndexed.Key, pointIndexed.Value);
                }
            }
            return edgesDict;
        }

        private static ICollection<Utils.Point> SortPointsOnEdge(Vector2d a, Vector2d b, IEnumerable<Utils.Point> points)
        {
            //упаковка
            var package = new Dictionary<int, Vector2d>();
            foreach (var point in points)
            {
                package.Add(point.ID, point.Coord);
            }
            //сортировка
            var sorted = Geometry2DHelper.SortPointsOnEdge(a, b, package);
            //распаковка
            var tmp = new Dictionary<int, Utils.Point>();
            foreach (var point in points)
            {
                tmp.Add(point.ID, point);
            }

            var result = new List<Utils.Point>();
            foreach (var sortedItem in sorted)
            {
                result.Add(tmp[sortedItem.Key]);
            }
            return result;
        }

        public static Dictionary<int, bool> AddNewEdges(SBRepObject sbrep, IntersectContour contour, Dictionary<int, int> pointIndexDict, Dictionary<int, IEnumerable<int>> edgesReplaceDict)
        {
            var addedEdgesIDs = new Dictionary<int, bool>();
            var addingEdges = contour.Edges.Where(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane).ToList();
            var segmentEdges = contour.Edges.Where(edge => edge.Position.Mode == ShortEdgePositionMode.EdgeSegment).ToList();
            var existingEdges = contour.Edges.Where(edge => edge.Position.Mode == ShortEdgePositionMode.ExistingEdge).ToList();
            foreach (var edge in addingEdges)
            {
                var newEdgeIndex = sbrep.AddEdge(pointIndexDict[edge.Points.a], pointIndexDict[edge.Points.b]);
                addedEdgesIDs.Add(newEdgeIndex, true);
            }
            foreach (var edge in existingEdges)
            {
                addedEdgesIDs.Add(edge.Position.EdgeId, false);
            }
            ///тут получается нужно взять все айдишники граней, которые уже есть в объекте, т.к. точки в грани мы вставляли ранее
            ///соответственно, нужен словарь, где и какое ребро заменяли
            foreach (var edge in segmentEdges)
            {
                var eid = edge.Position.EdgeId;
                if (!edgesReplaceDict.ContainsKey(eid))
                    throw new Exception("Да быть такого не может");
                var newEdges = edgesReplaceDict[eid];
                var indexAB = new Index2i(pointIndexDict[edge.Points.a], pointIndexDict[edge.Points.b]);
                var indexBA = new Index2i(pointIndexDict[edge.Points.b], pointIndexDict[edge.Points.a]);
                var id = newEdges.First(
                    nedge =>
                    sbrep.Edges[nedge].Vertices == indexAB ||
                    sbrep.Edges[nedge].Vertices == indexBA);
                addedEdgesIDs.Add(id, false);
            }
            return addedEdgesIDs;
        }
    }

}