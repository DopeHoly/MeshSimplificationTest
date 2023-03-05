﻿using g3;
using MeshSimplificationTest.SBRep.Utils;
using MeshSimplificationTest.SBRepVM;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using static g3.DPolyLine2f;

namespace MeshSimplificationTest.SBRep
{
    public static class SBRepOperationsExtension
    {

        private static IntersectContour GetContourFromLoop(SBRepObject sbrep, int lid)
        {
            var loopVertices = sbrep.GetClosedContourVtx(lid);
            var loopEdges = sbrep.GetClosedContourEdgesFromLoopID(lid);
            return IntersectContour.FromSBRepLoop(loopVertices, loopEdges);
        }

        private static IntersectContour GetProjectionContourFromPoint(List<Vector2d> contour)
        {
            if (contour == null || contour.Count < 3)
                throw new Exception("Контур не соответствует требованиям для проекции");

            if (contour.First() == contour.Last())
                contour.Remove(contour.Last());
            if (contour.Count < 3)
                throw new Exception("Контур не соответствует требованиям для проекции");

            var projectionContour = IntersectContour.FromPoints(contour);

            return projectionContour;
        }

        public static SBRepObject ContourProjection(this SBRepObject sbrep, List<Vector2d> contour, bool topDirection)
        {
            var obj = new SBRepObject(sbrep);
            if (obj.Faces.Count == 0)
                return obj;
            if (contour == null || contour.Count < 3)
                return obj;

            //валидация контура по площади, если нулевая, то ничего не делаем
            if (Geometry2DHelper.GetArea(contour) < 1e-6)
                return obj;

            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            int maxGroidID = obj.Faces.Select(face => face.GroupID).Max();
            var replaceIntdex = maxGroidID + 2;
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
            var projectionContour = GetProjectionContourFromPoint(contour);
            projectionContour = IntersectContour.Intersect(projectionContour, projectionContour);
            var count = 0;
            foreach (var face in filteredFaces)
            {
                if(face.ID == 128)
                {
                    ;
                }
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
                ApplyIntersectContourToFace(obj, face.ID, resultIntersect, groupIDsDict, ref maxGroidID, true);
                ++count;
            }

            obj.RebuildVerges();
            return obj;
        }

        public static SBRepObject ContourProjectionParallel(this SBRepObject sbrep, List<Vector2d> contour, bool topDirection)
        {
            var obj = new SBRepObject(sbrep);
            if (obj.Faces.Count == 0)
                return obj;
            if (contour == null || contour.Count < 3)
                return obj;

            //валидация контура по площади, если нулевая, то ничего не делаем
            if (Geometry2DHelper.GetArea(contour) < 1e-6)
                return obj;

            //выбираем только нужные грани
            Func<double, bool> comparer = null;
            if (topDirection)
                comparer = (x) => x > 0;
            else
                comparer = (x) => x < 0;

            int maxGroidID = obj.Faces.Select(face => face.GroupID).Max();
            var replaceIntdex = maxGroidID + 2;
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
            var projectionContour = GetProjectionContourFromPoint(contour);
            projectionContour = IntersectContour.Intersect(projectionContour, projectionContour);
            var facesCrossedContor = new List<int>();
            //foreach (var face in filteredFaces)
            object mutex = new object();

            Parallel.ForEach(filteredFaces,
                (face) =>
                {
                    var projContour = new IntersectContour(projectionContour);
                    var outsideLoop = new IntersectContour(GetContourFromLoop(obj, face.OutsideLoop));
                    var resultIntersect = IntersectContour.CheckCrosses(outsideLoop, projContour);

                    switch (resultIntersect)
                    {
                        case ContoursIntersectType.Inside:
                            lock (mutex)
                                face.GroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                            break;
                        case ContoursIntersectType.Outside:
                            break;
                        case ContoursIntersectType.PartlyInside:
                        case ContoursIntersectType.Cross:
                            lock (mutex)
                                facesCrossedContor.Add(face.ID);
                            break;
                        default:
                            break;
                    }
                });
            foreach (var faceID in facesCrossedContor)
            {
                var face = sbrep.Faces[faceID];
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
                ApplyIntersectContourToFace(obj, faceID, resultIntersect, groupIDsDict, ref maxGroidID, false);
            }
            obj.RebuildVerges();
            return obj;
        }

        private static void AddToSBrep(SBRepObject sbrep, IntersectContour contour, PlaneFace plane, Color color)
        {
            Dictionary<int, int> pointIndexDict = new Dictionary<int, int>();
            foreach (var point in contour.Points)
            {
                var point3D = new Vector3d(
                    point.Coord.x,
                    point.Coord.y,
                    plane.GetZ(point.Coord.x, point.Coord.y));

                int newIndex = sbrep.AddVertex(point3D);
                pointIndexDict.Add(point.ID, newIndex);
            }
            foreach (var edge in contour.Edges)
            {
                var index = sbrep.AddEdge(pointIndexDict[edge.Points.a], pointIndexDict[edge.Points.b]);
                sbrep.Edges[index].Color = color;
            }
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

        public static IEnumerable<SBRepObject> DebugContourProjection(this SBRepObject sbrep, List<Vector2d> contour, bool topDirection)
        {
            var obj = new SBRepObject(sbrep);
            if (obj.Faces.Count == 0)
                return new List<SBRepObject>();
            if (contour == null || contour.Count < 3)
                return new List<SBRepObject>();

            //валидация контура по площади, если нулевая, то ничего не делаем
            if (Geometry2DHelper.GetArea(contour) < 1e-6)
                return new List<SBRepObject>();
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
                return new List<SBRepObject>() { obj };
            //TODO тут нужно разделить конутр на N контуров без самопересечений, т.к. в исходном контуре они могут быть
            var projectionContour = GetProjectionContourFromPoint(contour);
            projectionContour = IntersectContour.Intersect(projectionContour, projectionContour);
            var count = 0;
            var results = new List<SBRepObject>();
            foreach (var face in filteredFaces)
            {
                obj = new SBRepObject(sbrep);
                var projContour = new IntersectContour(projectionContour);
                var outsideLoop = new IntersectContour(GetContourFromLoop(obj, face.OutsideLoop));
                //if (face.ID == 9)
                //    AddToSBrep(obj, outsideLoop, face.Plane, Colors.GreenYellow);
                //else
                //    AddToSBrep(obj, outsideLoop, face.Plane, Colors.AliceBlue);
                var insideLoops = face.InsideLoops.Select(lid =>
                 new IntersectContour(GetContourFromLoop(obj, lid)))
                    .ToList();
                var resultIntersect = IntersectContour.Intersect(projContour, outsideLoop);
                foreach (var insideLoop in insideLoops)
                {
                    //AddToSBrep(obj, insideLoop, face.Plane, Colors.BlueViolet);
                    resultIntersect = IntersectContour.Difference(resultIntersect, insideLoop);
                }
                try
                {

                    //AddToSBrep(obj, resultIntersect, face.Plane, Colors.Chocolate);
                    DebugApplyIntersectContourToFace(obj, face.ID, resultIntersect, groupIDsDict, ref maxGroidID);
                }
                catch
                {
                    results.Add(new SBRepObject(obj));
                    return results;
                }
                results.Add(new SBRepObject(obj));
                ++count;
            }

            return results;
        }

        public static void DebugApplyIntersectContourToFace(SBRepObject sbrep, int faceID, IntersectContour contour, Dictionary<int, int> groupIDsDict, ref int maxGroidID, bool useShortAlgorithm = false)
        {
            var face = sbrep.Faces[faceID];
            //Обработка случаев, когда полностью грань попадает в проекцию или не попадает
            if (useShortAlgorithm)
            {
                if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.OutPlane))
                {
                    var faceEdgesPos = GetEdgesInsideOutside(sbrep, faceID, contour);
                    if (faceEdgesPos.All(ePos => ePos.Value == true))
                        face.GroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                    return;
                }
            }


            //Обработка случая, когда попали проекцией полностью на контур, и ничего не задели
            //если все грани внутри и все точки внутри, то юзаем упрощённый алгоритм
            //if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane) &&
            //    contour.Points.All(
            //        point => point.Position.Mode == PointPositionMode.InPlane))
            //{
            //    AddContourOnFace(sbrep, faceID, contour, groupIDsDict, ref maxGroidID);
            //    return;
            //}

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

#if DEBUG
            foreach (var edge in sbrep.Edges)
            {
                edge.Color = Colors.Gray;
            }
#endif

            //вычисляем, какие рёбра внутри, какие снаружи
            var faceEdgesPosition = GetEdgesInsideOutside(sbrep, faceID, contour);

            //Если нет новых рёбер, лежащих внутри данной грани и нет рёбер грани, лежащих внутри, то ничего больше с гранью не делаем
            if (addedEdges.Where(x => x.Value == true).Count() == 0 && faceEdgesPosition
                .Where(idPos => idPos.Value == true).Count() == 0)
                return;

            //вычисляем множества рёбер для новых и старых граней
            var newFaceEdges = new List<int>();
            newFaceEdges.AddRange(
                faceEdgesPosition
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key));
            newFaceEdges.AddRange(addedEdges.Keys.ToList());

            IEnumerable<IEnumerable<int>> newFacesLoops = null;
            IEnumerable<IEnumerable<int>> oldFacesLoops = null;

            //Собираем петли для новых граней
            newFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, newFaceEdges);

#if DEBUG
            foreach (var eid in newFaceEdges)
            {
                sbrep.Edges[eid].Color = Colors.HotPink;
            }
            //foreach (var eid in oldFacesEdges)
            //{
            //    //Debug.Assert(sbrep.Edges[eid].Color != Colors.Gray);
            //    if (sbrep.Edges[eid].Color != Colors.Gray)
            //        sbrep.Edges[eid].Color = Colors.Yellow;
            //    else
            //        sbrep.Edges[eid].Color = Colors.Red;
            //}
#endif
            //Собираем петли для старых граней
            //случай, когда контур разделяет точками на грани на n петель
            if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane) &&
                contour.Points.Where(
                    point => point.Position.Mode == PointPositionMode.OnEdge ||
                    point.Position.Mode == PointPositionMode.OnVertex)
                .Count() > 1)
            {
                var oldFacesEdgesPriority = new Dictionary<int, bool>();

                foreach (var edge in faceEdgesPosition
                    .Where(idPos => idPos.Value == false)
                    .Select(idPos => idPos.Key))
                {
                    oldFacesEdgesPriority.Add(edge, false);
                }
                foreach (var edge in addedEdges
                    .Where(idPos => idPos.Value == true)
                    .Select(idPos => idPos.Key))
                {
                    oldFacesEdgesPriority.Add(edge, true);
                }
                oldFacesLoops = SBRepObject.BuildLoopsFromEdgesByAngle(sbrep, oldFacesEdgesPriority);
            }
            else
            {
                var oldFacesEdges = new List<int>();
                oldFacesEdges.AddRange(
                    faceEdgesPosition
                    .Where(idPos => idPos.Value == false)
                    .Select(idPos => idPos.Key));
                oldFacesEdges.AddRange(
                    addedEdges
                    .Where(idPos => idPos.Value == true)
                    .Select(idPos => idPos.Key));
#if DEBUG
                foreach (var eid in oldFacesEdges)
                {
                    //Debug.Assert(sbrep.Edges[eid].Color != Colors.Gray);
                    if (sbrep.Edges[eid].Color != Colors.Gray)
                        sbrep.Edges[eid].Color = Colors.Yellow;
                    else
                        sbrep.Edges[eid].Color = Colors.Red;
                }
#endif
                //Собираем петли для старых граней
                oldFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, oldFacesEdges);
            }

            return;


            Debug.Assert(newFacesLoops.All(x => x.Count() >= 3));
            Debug.Assert(oldFacesLoops.All(x => x.Count() >= 3));
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
                Debug.Assert(loopId != -1);
                loopsByNewFaces.Add(sbrep.Loops[loopId]);
            }
            var loopsByOldFaces = new IndexedCollection<SBRep_Loop>();
            foreach (var loop in oldFacesLoops)
            {
                if (loop.Count() == 0) continue;
                int loopId = -1;
                var loopVerges = loop.Select(eid => dictEdgeNewVerge[eid]).ToList();
                loopId = sbrep.AddLoop(loopVerges);
                Debug.Assert(loopId != -1);
                loopsByOldFaces.Add(sbrep.Loops[loopId]);
            }


            //дальше создаём по петлям все заготовки данных для грани (какие петли внутренние, какие внешние)

            var blackList = new List<int>();
            var blackListNew = new List<int>();
            var facesLoopsColectionOld = new Dictionary<int, IEnumerable<int>>();
            foreach (var loop in loopsByOldFaces)
            {
                IEnumerable<int> insideLoopsIds = new List<int>();
                var checkedLoops = loopsByOldFaces.Where(checkedLoop => checkedLoop != loop).ToList();
                insideLoopsIds = GetInsideLoops(sbrep, loop, checkedLoops);
                blackList.AddRange(insideLoopsIds);
                facesLoopsColectionOld.Add(loop.ID, insideLoopsIds);
            }
            var facesLoopsColectionNew = new Dictionary<int, IEnumerable<int>>();
            foreach (var loop in loopsByNewFaces)
            {
                IEnumerable<int> insideLoopsIds = new List<int>();
                var checkedLoops = loopsByNewFaces.Where(checkedLoop => checkedLoop != loop).ToList();
                insideLoopsIds = GetInsideLoops(sbrep, loop, checkedLoops);
                blackListNew.AddRange(insideLoopsIds);
                facesLoopsColectionNew.Add(loop.ID, insideLoopsIds);
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
                if (blackListNew.Contains(facedLoops.Key))
                    continue;
                var newGroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                sbrep.AddFace(newGroupID, face.Plane, face.Normal, facedLoops.Key, facedLoops.Value);
            }

            //пересобираем verges по принадлежности одинаковым граням и по зонам связанности
            sbrep.RebuildVerges();//TODO а надо ли
            ////////////////////

        }

        public static void ApplyIntersectContourToFace(SBRepObject sbrep, int faceID, IntersectContour contour, Dictionary<int, int> groupIDsDict, ref int maxGroidID, bool useShortAlgorithm = false)
        {
            var face = sbrep.Faces[faceID];
            //Обработка случаев, когда полностью грань попадает в проекцию или не попадает
            if (useShortAlgorithm)
            {
                if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.OutPlane))
                {
                    var faceEdgesPos = GetEdgesInsideOutside(sbrep, faceID, contour);
                    if (faceEdgesPos.All(ePos => ePos.Value == true))
                        face.GroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                    return;
                }
            }

            //if(faceID == 128)
            //{
            //    var outsideLoop = new IntersectContour(GetContourFromLoop(sbrep, face.OutsideLoop));
            //    SbrepVizualizer.ShowContours(sbrep, new List<IntersectContour>() { outsideLoop, contour });
            //    ShowOldNewEdgesPlot(sbrep, faceEdgesPosition, addedEdges);
            //}

            //Обработка случая, когда попали проекцией полностью на контур, и ничего не задели
            //если все грани внутри и все точки внутри, то юзаем упрощённый алгоритм
            //if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane) &&
            //    contour.Points.All(
            //        point => point.Position.Mode == PointPositionMode.InPlane))
            //{
            //    AddContourOnFace(sbrep, faceID, contour, groupIDsDict, ref maxGroidID);
            //    return;
            //}

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

            //Если нет новых рёбер, лежащих внутри данной грани и нет рёбер грани, лежащих внутри, то ничего больше с гранью не делаем
            if (addedEdges.Where(x => x.Value == true).Count() == 0 && faceEdgesPosition
                .Where(idPos => idPos.Value == true).Count() == 0)
                return;

            //вычисляем множества рёбер для новых и старых граней
            var newFaceEdges = new List<int>();
            newFaceEdges.AddRange(
                faceEdgesPosition
                .Where(idPos => idPos.Value == true /*|| idPos.Value == null*/)
                .Select(idPos => idPos.Key));
            newFaceEdges.AddRange(addedEdges.Keys.ToList());

            IEnumerable<IEnumerable<int>> newFacesLoops = null;
            IEnumerable<IEnumerable<int>> oldFacesLoops = null;

            //Собираем петли для новых граней
            //try
            //{
            //    //if (newFaceEdges.Count < 3)
            //    //{
            //    //    var keyValuePairs = new Dictionary<Color, IEnumerable<int>>();
            //    //    var oldFacesEdgesIds = faceEdgesPosition.Select(x => x.Key).ToList();
            //    //    keyValuePairs.Add(Colors.Yellow, oldFacesEdgesIds);
            //    //    keyValuePairs.Add(Colors.Red, addedEdges.Keys);

            //    //    SbrepVizualizer.ShowEdgePlot(sbrep, keyValuePairs, contour);
            //    //    return;
            //    //}
            //    newFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, newFaceEdges);
            //}
            //catch
            //{
            //    var outsideLoop = new IntersectContour(GetContourFromLoop(sbrep, face.OutsideLoop));
            //    SbrepVizualizer.ShowContours(sbrep, new List<IntersectContour>() { outsideLoop, contour });
            //    ShowOldNewEdgesPlot(sbrep, faceEdgesPosition, addedEdges);
            //    throw;
            //}

            //Debug.Assert(newFaceEdges.Count == newFacesLoops.Sum(x => x.Count()));

            //var oldFacesEdges = new List<int>();
            ////Собираем петли для старых граней
            ////случай, когда контур разделяет точками на грани на n петель
            //if (contour.Edges.All(edge => edge.Position.Mode == ShortEdgePositionMode.InPlane) &&
            //    contour.Points.Where(
            //        point => point.Position.Mode == PointPositionMode.OnEdge ||
            //        point.Position.Mode == PointPositionMode.OnVertex)
            //    .Count() > 1)
            //{
            //    var oldFacesEdgesPriority = new Dictionary<int, bool>();

            //    foreach (var edge in faceEdgesPosition
            //        .Where(idPos => idPos.Value == false)
            //        .Select(idPos => idPos.Key))
            //    {
            //        oldFacesEdgesPriority.Add(edge, false);
            //    }
            //    foreach (var edge in addedEdges
            //        .Where(idPos => idPos.Value == true)
            //        .Select(idPos => idPos.Key))
            //    {
            //        oldFacesEdgesPriority.Add(edge, true);
            //    }
            //    oldFacesLoops = SBRepObject.BuildLoopsFromEdgesByAngle(sbrep, oldFacesEdgesPriority);
            //}
            //else
            //{
            //    oldFacesEdges = new List<int>();
            //    oldFacesEdges.AddRange(
            //        faceEdgesPosition
            //        .Where(idPos => idPos.Value == false)
            //        .Select(idPos => idPos.Key));
            //    oldFacesEdges.AddRange(
            //        addedEdges
            //        .Where(idPos => idPos.Value == true)
            //        .Select(idPos => idPos.Key));

            //    //Собираем петли для старых граней
            //    try
            //    {
            //    oldFacesLoops = SBRepObject.BuildLoopsFromEdges(sbrep, oldFacesEdges);
            //    }
            //    catch
            //    {
            //        var outsideLoop = new IntersectContour(GetContourFromLoop(sbrep, face.OutsideLoop));
            //        SbrepVizualizer.ShowContours(sbrep, new List<IntersectContour>() { outsideLoop, contour });
            //        ShowOldNewEdgesPlot(sbrep, faceEdgesPosition, addedEdges);
            //        throw;
            //    }

            //    //Debug.Assert(oldFacesEdges.Count == oldFacesLoops.Sum(x => x.Count()));
            //}

            //if(!newFacesLoops.All(x => x.Count() >= 3) || !oldFacesLoops.All(x => x.Count() >= 3))
            //{
            //    var outsideLoop = new IntersectContour(GetContourFromLoop(sbrep, face.OutsideLoop));
            //    SbrepVizualizer.ShowContours(sbrep, new List<IntersectContour>() { outsideLoop , contour});

            //    throw new Exception();
            //}


            //Debug.Assert(newFacesLoops.All(x => x.Count() >= 3));
            //Debug.Assert(oldFacesLoops.All(x => x.Count() >= 3));

            //словарь, где ключ - номер ребра, значение - является ли ребро внутренним к внешнему контуру
            var edgesDict = new Dictionary<int, bool>();
            foreach (var contourEdge in addedEdges)
            {
                if (contourEdge.Value == false)
                    continue;
                edgesDict.Add(contourEdge.Key, false);
            }
            var facesEdges = sbrep.GetEdgesFromFaceId(faceID);
            foreach (var eid in facesEdges)
            {
                edgesDict.Add(eid, true);
            }

            var allLoops = BuildLoopsFromEdges(sbrep, faceID, edgesDict);

            var oldAndNewLoops = ClassifyLoops(sbrep, allLoops, contour);
            oldFacesLoops = oldAndNewLoops.Item1;
            newFacesLoops = oldAndNewLoops.Item2;

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
                Debug.Assert(loopId != -1);
                loopsByNewFaces.Add(sbrep.Loops[loopId]);
            }
            var loopsByOldFaces = new IndexedCollection<SBRep_Loop>();
            foreach (var loop in oldFacesLoops)
            {
                if (loop.Count() == 0) continue;
                int loopId = -1;
                var loopVerges = loop.Select(eid => dictEdgeNewVerge[eid]).ToList();
                loopId = sbrep.AddLoop(loopVerges);
                Debug.Assert(loopId != -1);
                loopsByOldFaces.Add(sbrep.Loops[loopId]);
            }


            //дальше создаём по петлям все заготовки данных для грани (какие петли внутренние, какие внешние)

            var blackList = new List<int>();
            var blackListNew = new List<int>();
            var facesLoopsColectionOld = new Dictionary<int, IEnumerable<int>>();
            foreach (var loop in loopsByOldFaces)
            {
                IEnumerable<int> insideLoopsIds = new List<int>();
                var checkedLoops = loopsByOldFaces.Where(checkedLoop => checkedLoop != loop).ToList();
                insideLoopsIds = GetInsideLoops(sbrep, loop, checkedLoops);
                blackList.AddRange(insideLoopsIds);
                facesLoopsColectionOld.Add(loop.ID, insideLoopsIds);
            }
            var facesLoopsColectionNew = new Dictionary<int, IEnumerable<int>>();
            foreach (var loop in loopsByNewFaces)
            {
                IEnumerable<int> insideLoopsIds = new List<int>();
                var checkedLoops = loopsByNewFaces.Where(checkedLoop => checkedLoop != loop).ToList();
                insideLoopsIds = GetInsideLoops(sbrep, loop, checkedLoops);
                blackListNew.AddRange(insideLoopsIds);
                facesLoopsColectionNew.Add(loop.ID, insideLoopsIds);
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
                if (blackListNew.Contains(facedLoops.Key))
                    continue;
                var newGroupID = GetNewGroupIDFromDictionary(face.GroupID, groupIDsDict, ref maxGroidID);
                sbrep.AddFace(newGroupID, face.Plane, face.Normal, facedLoops.Key, facedLoops.Value);
            }
        }

        private static void ShowOldNewEdgesPlot(SBRepObject sbrep, Dictionary<int, bool?> faceEdgesPosition, Dictionary<int, bool> addedEdges)
        {
            var keyValuePairs = new Dictionary<Color, IEnumerable<int>>();

            var oldEdgesOutside = faceEdgesPosition
                .Where(idPos => idPos.Value == false)
                .Select(idPos => idPos.Key);
            var oldEdgesOnEdge = faceEdgesPosition
                .Where(idPos => idPos.Value == null)
                .Select(idPos => idPos.Key);
            var oldEdgesIndide = faceEdgesPosition
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key);

            var newEdgesIndide = addedEdges
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key);
            var newEdgesOutside = addedEdges
                .Where(idPos => idPos.Value == false)
                .Select(idPos => idPos.Key);

            keyValuePairs.Add(Colors.Red, oldEdgesOutside);
            keyValuePairs.Add(Colors.Green, oldEdgesOnEdge);
            keyValuePairs.Add(Colors.Blue, oldEdgesIndide);

            keyValuePairs.Add(Colors.Black, newEdgesIndide);
            keyValuePairs.Add(Colors.Yellow, newEdgesOutside);


            SbrepVizualizer.ShowEdgePlot(sbrep, keyValuePairs);
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
                faceEdgesPosition.Add(edge, contour.EdgeInsideDeep(a, b, 1e-12));
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

        /// <summary>
        /// Сортирует точки на плоскости по полярному углу 
        /// </summary>
        /// <param name="obj">объект sbrep</param>
        /// <param name="vtxIds">индексы вершин</param>
        /// <param name="plane">плоскость, в которой лежат вершины</param>
        /// <param name="clockwise">сортировка по часовой/против часовой стрелки</param>
        /// <returns>сортированный список точек</returns>
        private static IEnumerable<int> SortByPolar(SBRepObject obj, IEnumerable<int> vtxIds, PlaneFace plane, bool clockwise, int curentVtx = -1, int prevVtx = -1)
        {
            Vector3d edgeCurentPoint = Vector3d.Zero;
            Vector3d edgePrevPoint = Vector3d.One;
            if(curentVtx != -1)
            {
                var lastEdgesPoint = obj.GetCoordinatesWithId(new List<int>() { curentVtx, prevVtx });
                edgeCurentPoint = lastEdgesPoint.First().Value;
                edgePrevPoint = lastEdgesPoint.Last().Value;
            }
            else
            {
                edgePrevPoint = new Vector3d(0, 1, plane.GetZ(0, 1));
            }

            var points = obj.GetCoordinatesWithId(vtxIds);

            var currentEdgeVector = edgeCurentPoint - edgePrevPoint;

            Func<Vector3d, double> calcAngle = (p1) =>
            {
                return signedAngle(currentEdgeVector, p1 - edgePrevPoint, plane.Normal);
            };

            var vtxAngleDict = new Dictionary<int, double>();
            foreach (var item in points)
            {
                vtxAngleDict.Add(item.Key, calcAngle(item.Value));
            }

            Func<double, double, int> sortFunc = (p1, p2) =>
            {
                return (clockwise ? -1 : 1) * p1.CompareTo(p2);
            };
            var vertices = new List<int>(vtxIds);
            vertices.Sort(delegate (int a, int b)
            {
                var p1 = vtxAngleDict[a];
                var p2 = vtxAngleDict[b];
                return sortFunc(p1, p2);
            });
            return vertices;
        }

        //public static double GetAngle(Vector2d v1, Vector2d v2)
        //{
        //    return Math.Acos(v1.Dot(v2)/(v1.Length * v2.Length)) * (180.0 / Math.PI);
        //}

        /// <summary>
        /// Функция получает угол со знаком между указанными векторами,
        /// относительно плоскости заданной нормалью
        /// </summary>
        public static double signedAngle(Vector3d v1, Vector3d v2, Vector3d norm)
        {
            Vector3d v3 = v1.Cross(v2);
            double angle = Math.Acos(v1.Normalized.Dot(v2.Normalized));
            return angle * Math.Sign(v3.Dot(norm));
        }

        //public static IEnumerable<Vector2d> SortByPolarT (IEnumerable<Vector2d> points, Vector2d zeroP, Vector2d lastP)
        //{
        //    var currentEdgeVector = lastP - zeroP;
        //    var current3d = new Vector3d(currentEdgeVector.x, currentEdgeVector.y, 0);
        //    var norm = new Vector3d(0, 0, 1);
        //    Func<Vector2d, double> calcAngle = (p1) =>
        //    {
        //        var p = p1 - zeroP;
        //        var p3d = new Vector3d(p.x, p.y, 0);
        //        return signedAngle(current3d, p3d, norm);
        //    };

        //    var dict = new Dictionary<Vector2d, double>();
        //    foreach (var item in points)
        //    {
        //        dict.Add(item, calcAngle(item) * (180.0 / Math.PI));
        //    }
        //    ;
        //    return null;
        //}

        private static Dictionary<int, IEnumerable<int>> GetVtxParentsDict(SBRepObject obj, IEnumerable<int> edgesIds)
        {
            var edges = edgesIds.Select(eid => obj.Edges[eid]).ToList();
            var verticesIds = edges.SelectMany(edge =>
                new int[2] { edge.Vertices.a, edge.Vertices.b }
                )
                .Distinct()
                .ToList();
            var vertices = verticesIds.Select(vid => obj.Vertices[vid]).ToList();

            //индексируем словарь, точка - рёбра, которые она объеденяет из списка edgesIds
            var vertParentsDict = new Dictionary<int, IEnumerable<int>>();
            foreach (var vtx in vertices)
            {
                var vertexParents = vtx.Parents.Intersect(edgesIds);
                vertParentsDict.Add(vtx.ID, vertexParents);
            }
            return vertParentsDict;
        }

        private static Dictionary<int, bool> MarksOutsideEdges(SBRepObject obj, IEnumerable<int> edgesIDs)
        {
            //нужно выбрать максимальное ребро на отдаление, потому что начальное ребро должно находиться точно на границе контура
            //TODO
            var dict = new Dictionary<int, bool>();
            foreach (var item in edgesIDs)
            {
                dict.Add(item, true);
            }

            //далее следует
            return dict;
        }

        private static void ShowDictEdges(SBRepObject obj, Dictionary<int, bool> edgesIDsWithPosition)
        {
            var keyValuePairs = new Dictionary<Color, IEnumerable<int>>();

            var boundary = edgesIDsWithPosition
                .Where(idPos => idPos.Value == true)
                .Select(idPos => idPos.Key);
            var inner = edgesIDsWithPosition
                .Where(idPos => idPos.Value == false)
                .Select(idPos => idPos.Key);

            keyValuePairs.Add(Colors.Red, boundary);
            keyValuePairs.Add(Colors.Blue, inner);

            SbrepVizualizer.ShowEdgePlot(obj, keyValuePairs);
        }


        private static IEnumerable<IEnumerable<int>> BuildLoopsFromEdges(SBRepObject obj, int faceId, Dictionary<int, bool> edgesIDsWithPosition)
        {
            ShowDictEdges(obj, edgesIDsWithPosition);
            if (edgesIDsWithPosition.Count < 3)
                throw new Exception("Количество граней меньше 3. Невозможно собрать петлю");


            var plane = obj.Faces[faceId].Plane;

            ICollection<int> edgesIds = null;
            Dictionary<int, IEnumerable<int>> vertParentsDict = null;

            var loops = new List<IEnumerable<int>>();
            var nextEdgeId = -1;
            var beginVtxId = -1;
            int currentVtxID = -1;
            int prevVtxID = -1;
            List<int> currentLoopEdges = null;
            var currentEdgePositionDict = new Dictionary<int, bool>(edgesIDsWithPosition);
            //пока есть непройденные рёбра
            while (currentEdgePositionDict.Count > 0)
            {
                int currentEdgeId = -1;
                //если не известно следующее ребро, то начинаем новую петлю
                if(nextEdgeId == -1)
                {
                    //подготавливаем данные для новой петли
                    edgesIds = currentEdgePositionDict.Select(eid => eid.Key).ToList();
                    //получаем словарь: вершина - рёбра из edgesIds, которые её содержат
                    vertParentsDict = GetVtxParentsDict(obj, edgesIds);

                    currentLoopEdges = new List<int>();
                    loops.Add(currentLoopEdges);

                    //начинаем с ребра, лежащего на границе
                    int edgeOnEdge = -1;
                    var boundaryEdge = currentEdgePositionDict
                        .Where(x => x.Value)
                        .Select(e => (int?)e.Key)
                        .FirstOrDefault();
                    if (boundaryEdge == null)
                    {
                        currentEdgePositionDict = MarksOutsideEdges(obj, currentEdgePositionDict.Keys);
                        boundaryEdge = boundaryEdge = currentEdgePositionDict
                            .Where(x => x.Value)
                            .Select(e => (int?)e.Key)
                            .FirstOrDefault();
                        Debug.Assert(boundaryEdge != null);
                    }
                    edgeOnEdge = (int)boundaryEdge;
                    Debug.Assert(edgeOnEdge != -1);
                    currentEdgeId = edgeOnEdge;

                    //сортируем точки ребра по полярному углу по часовой стрелке
                    var edgeVtxIds = obj.Edges[currentEdgeId].Vertices;
                    var sortedEdgeVtxIds = SortByPolar(
                        obj,
                        new List<int>() { edgeVtxIds.a, edgeVtxIds.b },
                        plane,
                        true);

                    //первую точку ребра по полярному углу устанавливаем, как начальную для петли
                    beginVtxId = sortedEdgeVtxIds.First();
                    prevVtxID = beginVtxId;
                    //вторую точку ребра устанавливаем, как текущую
                    currentVtxID = sortedEdgeVtxIds.Last();
                }
                else
                {
                    prevVtxID = currentVtxID;
                    //получаем новую точку обхода от предыдущей по новому ребру
                    currentVtxID = obj.GetVertexNeigborIdByEdge(currentVtxID, nextEdgeId);
                    currentEdgeId = nextEdgeId;
                }
                //удаляем из списка непройденных рёбер текущее
                edgesIds.Remove(currentEdgeId);
                currentLoopEdges.Add(currentEdgeId);
                //удаляем граничные, маркеруем бывшие не граничные, как граничные
                if (currentEdgePositionDict[currentEdgeId])
                    currentEdgePositionDict.Remove(currentEdgeId);
                else
                    currentEdgePositionDict[currentEdgeId] = true;

                ShowDictEdges(obj, currentEdgePositionDict);



                if (currentVtxID == beginVtxId)
                {
                    nextEdgeId = -1;
                    continue;
                }


                //дальше двигаемся к следующему ребру
                var parents = vertParentsDict[currentVtxID]
                    .Where(eid => edgesIds.Contains(eid))
                    .ToList();
                Debug.Assert(parents.Count != 0);
                // если нет развилки, то просто берём следующее
                if (parents.Count == 1)
                {
                    nextEdgeId = parents.First();
                }
                else //если текущая точка - развилка
                {                    
                    //получаем точки, куда можем пойти
                    var potentialPointsEdgeDict = new Dictionary<int, int>();
                    foreach (var eid in parents)
                    {
                        potentialPointsEdgeDict.Add(obj.GetVertexNeigborIdByEdge(currentVtxID, eid), eid);
                    }

                    // ортируем точки, до которых с текущей точки можем дойти по полярному углу против часовой 
                    var sortedVtxIds = SortByPolar(
                        obj,
                        potentialPointsEdgeDict.Keys,
                        plane,
                        false,
                        currentVtxID,
                        prevVtxID);

                    // берём первую и переходим по соответствующему ребру
                    nextEdgeId = potentialPointsEdgeDict[sortedVtxIds.First()];
                }
            }

            return loops;
        }

        private static bool ClassifyLoop(SBRepObject obj, IEnumerable<int> loop, IntersectContour contour)
        {
            //находим точку внутри контура
            var loopCoord = loop
                .Select(x => obj.Edges[x].Vertices)
                .SelectMany( x => new List<int>(){ x.a, x.b })
                .Distinct()
                .Select(x => obj.Vertices[x].Coordinate.xy)
                .ToList();

            var centerPoint = Vector2d.Zero;
            foreach (var loopPoint in loopCoord)
            {
                centerPoint += loopPoint;
            }
            centerPoint /= loopCoord.Count;

            var maxDistPoint = Vector2d.Zero;
            var maxDistance = double.MinValue;
            foreach (var loopPoint in loopCoord)
            {
                if((loopPoint - centerPoint).LengthSquared > maxDistance)
                    maxDistPoint = loopPoint;
            }

            var point = maxDistPoint + (centerPoint - maxDistPoint)*1e-4;
            //определяем, где точка находится внутри или снаружи контура
            var pointPosition = IntersectContour.CalcPointPosition(contour, point, 1e-10);
            Debug.Assert(pointPosition != null);
            Debug.Assert(pointPosition.Mode != PointPositionMode.InPlane || pointPosition.Mode != PointPositionMode.OutPlane);
            if (pointPosition.Mode == PointPositionMode.InPlane)
                return true;
            return false;
        }


        public static Tuple<IEnumerable<IEnumerable<int>>, IEnumerable<IEnumerable<int>>> ClassifyLoops(
            SBRepObject obj,
            IEnumerable<IEnumerable<int>> loops,
            IntersectContour contour)
        {
            ICollection<IEnumerable<int>> oldoops = new List<IEnumerable<int>>();
            ICollection<IEnumerable<int>> newLoops = new List<IEnumerable<int>>();

            foreach (var loop in loops)
            {
                if(ClassifyLoop(obj, loop, contour))
                    newLoops.Add(loop);
                else
                    oldoops.Add(loop);
            }

            return new Tuple<IEnumerable<IEnumerable<int>>, IEnumerable<IEnumerable<int>>>(oldoops, newLoops);
        }
    }
}
