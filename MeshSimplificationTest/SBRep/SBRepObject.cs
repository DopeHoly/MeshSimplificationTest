using CGALDotNet;
using g3;
using Net3dBool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace MeshSimplificationTest.SBRep
{

    public class SBRepObject
    {
        public IndexedCollection<SBRep_Vtx> _vertices { get; private set; }
        public IndexedCollection<SBRep_Edge> _edges { get; private set; }
        public IndexedCollection<SBRep_LoopEdge> _loopPart { get; private set; }
        public IndexedCollection<SBRep_Loop> _loops { get; private set; }
        public IndexedCollection<SBRep_Face> _faces { get; private set; }

        public SBRepObject()
        {
            _vertices = new IndexedCollection<SBRep_Vtx>();
            _edges = new IndexedCollection<SBRep_Edge>();
            _loopPart = new IndexedCollection<SBRep_LoopEdge>();
            _loops = new IndexedCollection<SBRep_Loop>();
            _faces = new IndexedCollection<SBRep_Face>();
        }

        public void DefineFacesOutsideLoop()
        {
            foreach (var face in _faces)
            {
                if (face.InsideLoops.Count == 0)
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

        public void RedefineFeedbacks()
        {
            foreach (var loop in _loops)
            {
                loop.Parents = new List<int>();
            }
            foreach (var loopPart in _loopPart)
            {
                loopPart.Parents = new List<int>();
            }
            foreach (var edge in _edges)
            {
                edge.Parent = -1;
            }
            foreach (var vtx in _vertices)
            {
                vtx.Parents = new List<int>();
            }

            foreach (var face in _faces)
            {
                //_loops[face.OutsideLoop].Parents.Add(face.ID);
                foreach (var lid in face.InsideLoops)
                {
                    _loops[lid].Parents.Add(face.ID);
                }
            }

            foreach (var loop in _loops)
            {
                foreach (var leid in loop.LoopEdges)
                {
                    _loopPart[leid].Parents.Add(loop.ID);
                }
            }

            foreach (var le in _loopPart)
            {
                foreach (var eid in le.Edges)
                {
                    if (_edges[eid].Parent != -1)
                        throw new Exception();
                    _edges[eid].Parent = le.ID;
                }
            }

            foreach (var edge in _edges)
            {
                _vertices[edge.Vertices.a].Parents.Add(edge.ID);
                _vertices[edge.Vertices.b].Parents.Add(edge.ID);
            }
        }

        #region Data Access
        public IEnumerable<SBRep_Face> GetFaces()
        {
            return _faces;
        }
        public IEnumerable<int> GetFacesIds()
        {
            return _faces.GetIndexes();
        }

        public SBRep_Face GetFace(int id)
        {
            return _faces[id];
        }

        public Vector3d GetVertex(int id)
        {
            return _vertices[id].Coordinate;
        }

        public IEnumerable<Vector3d> GetVertices()
        {
            return _vertices.Select(x => x.Coordinate);
        }
        public IEnumerable<int> GetVerticesIds()
        {
            return _vertices.GetIndexes();
        }

        public IEnumerable<int> GetEdgesIdFromLoopId(int lid)
        {
            if (!_loops.ContainsKey(lid))
                return null;
            return _loops[lid].LoopEdges
                .SelectMany(x => _loopPart[x].Edges)
                .Distinct()
                .ToList();
        }
        #endregion

        public IEnumerable<Vector3d> GetClosedContour(int lid)
        {

            if (!_loops.ContainsKey(lid))
                return null;
            return GetClosedContourVtx(lid).Select(x => x.Coordinate).ToList();
        }

        public IEnumerable<SBRep_Vtx> GetClosedContourVtx(int lid)
        {
            if (!_loops.ContainsKey(lid))
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

                var edge = _edges[currentEdge];
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
                var vtx = _vertices[vtxID];
                vertexOrderList.Add(vtx);
                lastVtxID = vtx.ID;

                var nextEdge = vtx.Parents.Where(eid => eid != currentEdge && edgesIDs.Contains(eid)).ToList();
                if (nextEdge.Count != 1)
                    throw new Exception("");
                currentEdge = nextEdge.First();
            }

            return vertexOrderList;
        }

        /// <summary>
        /// Площадь внутри петли
        /// </summary>
        /// <param name="lid">Индекс петли</param>
        /// <returns></returns>
        public double GetLoopArea(int lid)
        {
            var contour = GetClosedContour(lid);
            var loop = _loops[lid];
            var parentid = loop.Parents.FirstOrDefault();
            var face = _faces[parentid];
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
