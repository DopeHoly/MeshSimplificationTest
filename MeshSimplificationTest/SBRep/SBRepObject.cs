using g3;
using MeshSimplificationTest.SBRep.SBRepOperations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest.SBRep
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
            //TODO
        }

        public int AddVertex(Vector3d vertex)
        {
            var newVertex = new SBRep_Vtx()
            {
                Coordinate = vertex,
            };
            Vertices.Add(newVertex);
            return newVertex.ID;
        }

        public int NewEdge(int indexA, int indexB)
        {
            if(!(Vertices.ContainsKey(indexA) && Vertices.ContainsKey(indexB))) return -1;
            var newEdge = new SBRep_Edge()
            {
                Vertices = new Index2i(indexA, indexB)
            };
            Edges.Add(newEdge);
            Vertices[indexA].Parents.Add(newEdge.ID);
            Vertices[indexB].Parents.Add(newEdge.ID);
            return newEdge.ID;
        }

        public void RemoveEdge(SBRep_Edge edge)
        {
            var indexA = edge.Vertices.a;
            var indexB = edge.Vertices.b;
            Vertices[indexA].Parents.Remove(edge.ID);
            Vertices[indexB].Parents.Remove(edge.ID);

            var parent = edge.Parent;
            var parentVerge = Verges[parent];
            parentVerge.Edges.Remove(edge.ID);
            Edges.Remove(edge);
        }

        /// <summary>
        /// Добавляет точки vertices в ребро с индексом eid
        /// </summary>
        /// <param name="eid">индекс грани</param>
        /// <param name="vertices">список точек с индексами</param>
        /// <returns>словарь (старый индекс вершины, новый индекс вершины)</returns>
        public Dictionary<int,int> AddPointsOnEdge(int eid, IEnumerable<IIndexedVector3d> vertices)
        {
            if(!Edges.ContainsKey(eid)) return null;
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
                    currentPointId = pointsIndexesDictionary[i];
                NewEdge(previewsPointId, currentPointId);
                previewsPointId = currentPointId;
            }

            RemoveEdge(edge);
            foreach (var addedEdge in edges)
            {
                parentVerge.Edges.Add(addedEdge);
            }
            return pointsIndexesDictionary;
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
                        if(area > maxArea)
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
                //_loops[face.OutsideLoop].Parents.Add(face.ID);
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
            var loops = new List<int>();
            loops.Add(Faces[faceId].OutsideLoop);
            loops.AddRange(Faces[faceId].InsideLoops);
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
        public IEnumerable<Index2i> GetEdgesVtxs(IEnumerable<int> edgeIds)
        {
            return edgeIds.Select(eid => Edges[eid].Vertices).ToList();
        }

        #endregion

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
                if (nextEdge.Count != 1)
                    throw new Exception("У петли почему то появилась развилка");
                currentEdge = nextEdge.First();
            }

            return vertexOrderList;
        }
        /// <summary>
        /// Получить замкнутый контур из петли под индексом lid
        /// </summary>
        /// <param name="lid">индекс петли</param>
        /// <returns>упорядоченный список Объектов вершин</returns>
        public IEnumerable<SBRep_Edge> GetClosedContourEdges(int lid)
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
            return GetArea(contour2d);
        }

        /// <summary>
        /// Вычисляет двухмерные координаты трёхмерного контура
        /// </summary>
        /// <param name="contour">плоский контур с 3д координатами</param>
        /// <param name="normal">нормаль поверхности, на которой находится контур</param>
        /// <returns></returns>
        public List<Vector2d> ConvertPlaneContourTo2D(IEnumerable<Vector3d> contour, Vector3d normal)
        {
            //TODO заюзать алгоритм из SBRepToMeshBuilder ConvertTo2D
            var firstVtx = contour.First();
            var contourZero = contour.Select(x => x - firstVtx).ToList();

            var NormalX = new Vector3d(0, normal.y, normal.z);
            var Normaly = new Vector3d(0, 0, normal.z);

            var angleX = Vector3d.AngleD(NormalX, normal);
            var angleY = Vector3d.AngleD(Normaly, normal);

            Transform3DGroup rotateMtx = new Transform3DGroup();
            rotateMtx.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), angleY)));
            rotateMtx.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), angleX)));

            var point3D = contourZero.Select(x => 
            {
                var vtx = rotateMtx.Transform(
                    new Vector3D(x.x, x.y, x.z)
                    );
                return new Vector3d(vtx.X, vtx.Y, vtx.Z);
            }
            ).ToList();
            var points2d = point3D.Select(x => new Vector2d(x.x, x.y)).ToList();
            return points2d;
        }

        /// <summary>
        /// Вычисляет площадь внутри двухмерного контура points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double GetArea(List<Vector2d> points)
        {
            var points2D = new List<Vector2d>();
            var first = points.First();
            points2D.Add(points.Last());
            points2D.AddRange(points);
            points2D.Add(first);

            var cnt = points2D.Count - 2;
            decimal sum = 0.0M;
            for (int i = 1; i <= cnt; ++i)
            {
                sum += (decimal)points2D[i].x * ((decimal)points2D[i + 1].y - (decimal)points2D[i - 1].y);
            }
            var area = Math.Abs((double)sum) / 2.0;
            return area;
        }

    }
}
