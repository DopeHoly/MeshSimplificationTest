using g3;
using HelixToolkit.Logger;
using MeshSimplificationTest.SBRep.Triangulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Xaml;

namespace MeshSimplificationTest.SBRep
{
    public static class SBRepToMeshBuilder
    {
        public const double EPS_PointCompare = 1e-8;

        /// <summary>
        /// Функция проверки равнозначности векторов
        /// Проверяет с погрешностью EPS_NormalCompare
        /// </summary>
        /// <param name="a">Вектор первый</param>
        /// <param name="b">Вектор второй</param>
        /// <returns></returns>
        private static bool Vector3dEqual(Vector3d a, Vector3d b)
        {
            return Math.Abs(a.x - b.x) < EPS_PointCompare &&
                Math.Abs(a.y - b.y) < EPS_PointCompare &&
                Math.Abs(a.z - b.z) < EPS_PointCompare;
        }

        public struct FacedTriangle
        {
            public Index3i vertex;
            public int GroupID;
            public Vector3d normal;

            public void FixNormal(IEnumerable<Vector3d> vertices)
            {
                var v = vertices.ElementAt(vertex.a);
                var v2 = vertices.ElementAt(vertex.b);
                var v3 = vertices.ElementAt(vertex.c);
                var normalCalc = MathUtil.Normal(v, v2, v3);
                if(Math.Sign(normal.x) != Math.Sign(normalCalc.x) ||
                    Math.Sign(normal.y) != Math.Sign(normalCalc.y) ||
                    Math.Sign(normal.z) != Math.Sign(normalCalc.z))
                {
                    vertex = new Index3i(vertex.b, vertex.a, vertex.c);
                }
            }
        }

        public class FacedTriangulateData
        {
            public IEnumerable<FacedTriangle> triangles;
            public IEnumerable<Vector3d> vertices;

            public IEnumerable<FacedTriangle> ReindexTrianglesVertices(Dictionary<int, int> verticeIndexes)
            {
                return triangles.Select(triangle =>
                {
                    var vertices = new Index3i()
                    {
                        a = verticeIndexes[triangle.vertex.a],
                        b = verticeIndexes[triangle.vertex.b],
                        c = verticeIndexes[triangle.vertex.c],
                    };
                    return new FacedTriangle
                    {
                        vertex = vertices,
                        GroupID = triangle.GroupID,
                        normal = triangle.normal,
                    };
                });
            }
        }

        public static Tuple<Matrix3d, Matrix3d, Vector3d> CalculateTransform(
            IEnumerable<Vector3d> contour, 
            Vector3d normal)
        {
            var pointOnVector = contour.First();

            normal = normal.Normalized;
            Vector3d vectorM = Vector3d.Zero;
            if (Math.Abs(normal.x) > Math.Max(normal.y, normal.z))
            {
                vectorM = new Vector3d(normal.y, -normal.x, 0.0);
            }
            else
            {
                vectorM = new Vector3d(0, normal.z, -normal.y);
            }

            vectorM = vectorM.Normalized;
            var vectorP = vectorM.Cross(normal);

            var mtx = new Matrix3d(vectorP, vectorM, normal, true);
            var inverseMtx = new Matrix3d(vectorP, vectorM, normal, false);
            return new Tuple<Matrix3d, Matrix3d, Vector3d>(mtx, inverseMtx, pointOnVector);
        }

        public static IEnumerable<Vector3d> RevertTo3D(
            IEnumerable<Vector2d> contour,
            Matrix3d mtx,
            Vector3d offset)
        {
            var points3d = contour.Select(x => new Vector3d(x.x, x.y, 0.0)).ToList();
            var revert3d = points3d.Select(x =>
            {
                var point = mtx * x;
                return new Vector3d(point.x + offset.x, point.y + offset.y, point.z + offset.z);
            }
            ).ToList();
            return revert3d;
        }
        public static IEnumerable<Vector2d> ConvertTo2D(
            IEnumerable<Vector3d> contour,
            Matrix3d mtx,
            Vector3d offset)
        {
            var point3D = contour.Select(x =>
            {
                var point = new Vector3d(x.x - offset.x, x.y - offset.y, x.z - offset.z);
                return mtx * point;
            }
            ).ToList();
            var points2d = point3D.Select(x => new Vector2d(x.x, x.y)).ToList();
            return points2d;
        }

        public static Tuple<IEnumerable<Vector2d>, IEnumerable<Index3i>> Triangulate(
            IEnumerable<Vector2d> vertices, 
            IEnumerable<Index2i> edges)
        {
            IEnumerable<g3.Vector2d> triVertices = null;
            IEnumerable<g3.Index3i> triangles = null;

            var noError = TriangulationCaller.CDT(
                vertices, edges,
                out triVertices, out triangles);

            return new Tuple<IEnumerable<Vector2d>, IEnumerable<Index3i>>(triVertices, triangles);
        }

        public static int GetVtxHash(Vector3d vector)
        {
            const int digits = 7;
            //int hCode = 
            //    Math.Round(vector.x, digits).GetHashCode() ^
            //    Math.Round(vector.y, digits).GetHashCode() ^
            //    Math.Round(vector.z, digits).GetHashCode()
            //    ;
            var hCode = new Vector3d(
                Math.Round(vector.x, digits),
                Math.Round(vector.y, digits),
                Math.Round(vector.z, digits)
                ).GetHashCode();
            return hCode;
        }

        public static Tuple<IEnumerable<Vector3d>, IEnumerable<FacedTriangle>> ReindexTriangulateDatas(
            IEnumerable<FacedTriangulateData> triangulateDatas)
        {
            var vertices = new List<Vector3d>();
            var triangles = new List<FacedTriangle>();
            var verticesGlobalIndex = 0;
            foreach (var triData in triangulateDatas)
            {
                var vtxIndexes = new Dictionary<int, int>();
                var vtxHashs = new Dictionary<int, int>();
                var currentIndex = 0;
                foreach (var vertice in triData.vertices)
                {
                    //TODO Оптимизацияя
                    var verticeIndex = vertices.FindIndex(vtx => Vector3dEqual(vtx, vertice));
                    //var verticeIndex = -1;
                    //var hash = GetVtxHash(vertice);
                    //if(vtxHashs.ContainsKey(hash))
                    //    verticeIndex = vtxHashs[hash];
                    if (verticeIndex == -1)
                    {
                        vtxIndexes.Add(currentIndex, verticesGlobalIndex);
                        vertices.Add(vertice);
                        ++verticesGlobalIndex;
                    }
                    else
                    {
                        vtxIndexes.Add(currentIndex, verticeIndex);
                    }
                    ++currentIndex;
                }
                triangles.AddRange(triData.ReindexTrianglesVertices(vtxIndexes));

            }
            return new Tuple<IEnumerable<Vector3d>, IEnumerable<FacedTriangle>>(vertices, triangles);
        }


        private static Tuple<IEnumerable<Vector3d>, IEnumerable<Index2i>> GetVtxAndEdgesFromFace(
            SBRepObject sBRepObject, 
            int faceid)
        {
            var face = sBRepObject.Faces[faceid];
            var outsideLoop = face.OutsideLoop;
            var insideLoops = face.InsideLoops;
            var edgesIDs = new List<int>();
            var outsideEdges = sBRepObject.GetEdgesIdFromLoopId(outsideLoop);
            var insideEdges = insideLoops.SelectMany(lid => sBRepObject.GetEdgesIdFromLoopId(lid));

            edgesIDs.AddRange(outsideEdges);
            edgesIDs.AddRange(insideEdges);

            var vertexIds = sBRepObject.GetVerticesFromEdgesIds(edgesIDs);
            var vertices = sBRepObject.GetCoordinates(vertexIds);
            var edges = sBRepObject.GetEdgesVtxs(edgesIDs);

            //тут edge должны ссылаться на массив vertices по индексу, а тут это не так, так что тут нужна переиндексация
            var vtxIndexes = new Dictionary<int, int>();
            var index = 0;
            foreach(var vid in vertexIds)
            {
                vtxIndexes.Add(vid, index);
                ++index;
            }
            var newEdges = edges.Select(edge =>
            {
                return new Index2i()
                {
                    a = vtxIndexes[edge.a],
                    b = vtxIndexes[edge.b],
                };
            });

            return new Tuple<IEnumerable<Vector3d>, IEnumerable<Index2i>>(vertices, newEdges);
        }

        public static DMesh3 Convert(SBRepObject sBRepObject)
        {
            ///по каждой плоскости: 
            ///     разворчиваем параллельно плоскости XOY и смещаем в z = 0,
            ///     проводим триангуляцию и получаем список точек и треугольники
            ///     обратно преобразуем точки до прежнего состояния
            ///дальше реиндексируем точки и треугольники, присваиваем треугольникам id группы
            ///дальше пихаем это всё в билдер мэша

            var facedTriangulateData = new List<FacedTriangulateData>();

            foreach (var face in sBRepObject.Faces)
            {
                var faceData = GetVtxAndEdgesFromFace(sBRepObject, face.ID);
                var vertices = faceData.Item1;
                var edges = faceData.Item2;

                var transformMtxs = CalculateTransform(vertices, face.Normal);
                var vertice2D = ConvertTo2D(vertices, transformMtxs.Item1, transformMtxs.Item3);
                var triangulateData = Triangulate(vertice2D, edges);
                var vertice3D = RevertTo3D(triangulateData.Item1, transformMtxs.Item2, transformMtxs.Item3);
                var triangles = triangulateData.Item2;

                facedTriangulateData.Add(
                    new FacedTriangulateData()
                    {
                        vertices = vertice3D,
                        triangles = triangles.Select(
                            triangle => new FacedTriangle()
                            {
                                vertex = triangle,
                                GroupID = face.GroupID,
                                normal = face.Normal
                            })
                    });
            }

            var resultMeshData = ReindexTriangulateDatas(facedTriangulateData);

            var meshVertices = resultMeshData.Item1;

            foreach (var tri in resultMeshData.Item2)
            {
                tri.FixNormal(meshVertices);
            }

            var indexedTriangles = resultMeshData.Item2.Select(triData => triData.vertex);
            var faces = resultMeshData.Item2.Select(triData => triData.GroupID);
            var normals = resultMeshData.Item2.Select(triData =>new Vector3f(triData.normal));

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                meshVertices,
                indexedTriangles,
                TriGroups: faces
                );
            return mesh;
        }
    }
}
