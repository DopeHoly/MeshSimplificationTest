using g3;
using HelixToolkit.Wpf;
using MeshSimplificationTest.SBRep.Utils;
using MeshSimplificationTest.SBRepVM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static g3.DPolyLine2f;
using static MeshSimplificationTest.SBRep.SBRepToMeshBuilder;

namespace MeshSimplificationTest.SBRep
{
    public static class SBRepToMeshBuilderV2
    {
        public const double EPS_PointCompare = 1e-4;

        public class IndexedVertex : IIndexed
        {
            public int ID { get; set; } = -1;
            public Vector3d Coord { get; set; }
        }
        public class IndexedPoint: IIndexed
        {
            public int ID { get; set; } = -1;
            public Vector2d Coord { get; set; }
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
                if (Math.Sign(normal.x) != Math.Sign(normalCalc.x) ||
                    Math.Sign(normal.y) != Math.Sign(normalCalc.y) ||
                    Math.Sign(normal.z) != Math.Sign(normalCalc.z))
                {
                    vertex = new Index3i(vertex.b, vertex.a, vertex.c);
                }
            }
        }

        public static IndexedVertex To3D(
            IndexedPoint point, 
            Matrix3d mtx,
            Vector3d offset)
        {
            var vector = new Vector3d(point.Coord.x, point.Coord.y, 0.0);

            var point3d = mtx * vector;
            var offsetApply = new Vector3d(point3d.x + offset.x, point3d.y + offset.y, point3d.z + offset.z);
            return new IndexedVertex()
            {
                Coord = offsetApply,
                ID = point.ID,
            };
        }

        public static IndexedCollection<IndexedPoint> ConvertTo2D(
            IndexedCollection<IndexedVertex> contour,
            Matrix3d mtx,
            Vector3d offset)
        {
            var points2d = new IndexedCollection<IndexedPoint>();
            foreach (var vertice in contour)
            {
                //смещение в 0
                var point = new Vector3d(vertice.Coord.x - offset.x, vertice.Coord.y - offset.y, vertice.Coord.z - offset.z);
                //применение матрицы преобразования в плоскость XOY
                var point2D = mtx * point;
                //Добавление в список двухмерных точек
                points2d.Add(new IndexedPoint()
                {
                    ID = vertice.ID,
                    Coord = new Vector2d(point2D.x, point2D.y),
                });
            }
            return points2d;
        }

        public static IEnumerable<Index3i> Triangulate(
            IndexedCollection<IndexedPoint> vertices,
            IEnumerable<Index2i> edges,
            IndexedCollection<IndexedVertex> allVertices,
            Func<IndexedPoint, IndexedVertex> To3d,
            object mutex = null
            )
        {
            IEnumerable<g3.Vector2d> triVertices = null;
            IEnumerable<g3.Index3i> triangles = null;
            
            var verticeForTriangulation = vertices.Select(x => x.Coord).ToList();

            var vtxIndexes = new Dictionary<int, int>();
            var index = 0;
            foreach (var vid in vertices)
            {
                vtxIndexes.Add(vid.ID, index);
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

            var noError = TriangulationCaller.CDT(
                verticeForTriangulation, newEdges,
                out triVertices, out triangles);

            Debug.Assert(noError);

            var replaceIndexDict = new Dictionary<int, int>();
            var counter = 0;
            index = -1;
            var usedPoints = new List<int>();
            foreach (var point in triVertices)
            {
                IndexedPoint existingPoint = null;

                var minDistance = double.MaxValue;
                foreach (var item in vertices)
                {
                    if (usedPoints.Contains(item.ID))
                        continue;
                    var distance = (item.Coord - point).LengthSquared;
                    if (distance < minDistance)
                    {
                        existingPoint = item;
                        minDistance = distance;
                    }
                }

                if(minDistance > 1e-2)
                    existingPoint = null;
                //existingPoint = vertices.FirstOrDefault(x => Geometry2DHelper.EqualPoints(x.Coord, point, EPS_PointCompare));

                if(existingPoint == null)
                {
                    var newVertice = new IndexedPoint()
                    {
                        Coord = point
                    };
                    var vertex3d = To3d(newVertice);

                    lock (mutex)
                        allVertices.Add(vertex3d);
                    index = vertex3d.ID;
                }
                else
                {
                    usedPoints.Add(existingPoint.ID);
                    index = existingPoint.ID;
                }
                replaceIndexDict.Add(counter, index);
                ++counter;
            }

            var reindexTriangles = new List<g3.Index3i>();
            foreach (var tri in triangles)
            {
                var reindexTri = new Index3i()
                {
                    a = replaceIndexDict[tri.a],
                    b = replaceIndexDict[tri.b],
                    c = replaceIndexDict[tri.c],
                };
                reindexTriangles.Add(reindexTri);
            }

            return reindexTriangles;
        }

        public static Tuple<IEnumerable<Vector3d>, IEnumerable<FacedTriangle>> CompaqTriangleData(
            IndexedCollection<IndexedVertex> vertices,
            IEnumerable<FacedTriangle> triangles)
        {
            var replaceIndexDict = new Dictionary<int, int>();
            var resultVertices = new List<Vector3d>();
            var counter = 0;
            foreach (var vertex in vertices)
            {
                resultVertices.Add(vertex.Coord);
                replaceIndexDict.Add(vertex.ID, counter);
                ++counter;
            }

            var reindexTriangles = new List<FacedTriangle>();
            foreach (var tri in triangles)
            {
                var reindexTri = new Index3i()
                {
                    a = replaceIndexDict[tri.vertex.a],
                    b = replaceIndexDict[tri.vertex.b],
                    c = replaceIndexDict[tri.vertex.c],
                };
                var replaceTri = new FacedTriangle()
                {
                    vertex= reindexTri,
                    GroupID = tri.GroupID,
                    normal = tri.normal
                };
                reindexTriangles.Add(replaceTri);
            }
            return new Tuple<IEnumerable<Vector3d>, IEnumerable<FacedTriangle>>(resultVertices, reindexTriangles);
        }


        private static Tuple<IndexedCollection<IndexedVertex>, IEnumerable<Index2i>> GetVtxAndEdgesFromFace(
            SBRepObject sBRepObject,
            int faceid,
            IndexedCollection<IndexedVertex> vertices)
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
            var edges = sBRepObject.GetEdgesVtxs(edgesIDs);

            IndexedCollection<IndexedVertex> filteredVtx = new IndexedCollection<IndexedVertex>();

            foreach (var vid in vertexIds)
            {
                filteredVtx.Add(vertices[vid]);
            }

            return new Tuple<IndexedCollection<IndexedVertex>, IEnumerable<Index2i>>(filteredVtx, edges);
        }

        public static DMesh3 Convert(SBRepObject sBRepObject)
        {
            //сначала индексируем существующие точки в объекте
            var meshVertices = new IndexedCollection<IndexedVertex>();
            var triangles = new List<FacedTriangle>();

            foreach (var vertex in sBRepObject.Vertices)
            {
                meshVertices.Add(new IndexedVertex()
                {
                    ID = vertex.ID,
                    Coord = vertex.Coordinate
                });
            }

            foreach (var face in sBRepObject.Faces)
            {
                var faceData = GetVtxAndEdgesFromFace(sBRepObject, face.ID, meshVertices);
                var vertices = faceData.Item1;
                var edges = faceData.Item2;

                var transformMtxs = Geometry2DHelper.CalculateTransform(vertices.First().Coord, face.Normal);
                var vertice2D = ConvertTo2D(vertices, transformMtxs.Item1, transformMtxs.Item3);
                var faceTriangles = Triangulate(vertice2D, edges, meshVertices, x => To3D(x, transformMtxs.Item2, transformMtxs.Item3));

                triangles.AddRange(faceTriangles.Select(
                            triangle => new FacedTriangle()
                            {
                                vertex = triangle,
                                GroupID = face.GroupID,
                                normal = face.Normal
                            }).ToList());
            }

            var resultMeshData = CompaqTriangleData(meshVertices, triangles);
            var meshVerticesReindex = resultMeshData.Item1;
            var meshTriReindex = resultMeshData.Item2;

            foreach (var tri in meshTriReindex)
            {
                tri.FixNormal(meshVerticesReindex);
            }

            var indexedTriangles = meshTriReindex.Select(triData => triData.vertex).ToList();
            var faces = meshTriReindex.Select(triData => triData.GroupID).ToList();
            var normals = meshTriReindex.Select(triData => new Vector3f(triData.normal)).ToList();

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                meshVerticesReindex,
                indexedTriangles,
                TriGroups: faces
                );
            return mesh;
        }

        public static DMesh3 ConvertParallel(SBRepObject sBRepObject)
        {
            //сначала индексируем существующие точки в объекте
            var meshVertices = new IndexedCollection<IndexedVertex>();
            var triangles = new List<FacedTriangle>();

            foreach (var vertex in sBRepObject.Vertices)
            {
                meshVertices.Add(new IndexedVertex()
                {
                    ID = vertex.ID,
                    Coord = vertex.Coordinate
                });
            }

            object mutex = new object();

            Parallel.ForEach<SBRep_Face>(sBRepObject.Faces,
            (face) =>
            {
                var faceData = GetVtxAndEdgesFromFace(sBRepObject, face.ID, meshVertices);
                var vertices = faceData.Item1;
                var edges = faceData.Item2;

                var transformMtxs = Geometry2DHelper.CalculateTransform(vertices.First().Coord, face.Normal);
                var vertice2D = ConvertTo2D(vertices, transformMtxs.Item1, transformMtxs.Item3);
                var faceTriangles = Triangulate(
                    vertice2D, 
                    edges, 
                    meshVertices,  
                    x => To3D(x, transformMtxs.Item2, transformMtxs.Item3),
                    mutex
                    );

                lock (mutex)
                    triangles.AddRange(faceTriangles.Select(
                            triangle => new FacedTriangle()
                            {
                                vertex = triangle,
                                GroupID = face.GroupID,
                                normal = face.Normal
                            }).ToList());
            });

            var resultMeshData = CompaqTriangleData(meshVertices, triangles);
            var meshVerticesReindex = resultMeshData.Item1;
            var meshTriReindex = resultMeshData.Item2;

            foreach (var tri in meshTriReindex)
            {
                tri.FixNormal(meshVerticesReindex);
            }

            var indexedTriangles = meshTriReindex.Select(triData => triData.vertex).ToList();
            var faces = meshTriReindex.Select(triData => triData.GroupID).ToList();
            var normals = meshTriReindex.Select(triData => new Vector3f(triData.normal)).ToList();

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                meshVerticesReindex,
                indexedTriangles,
                TriGroups: faces
                );
            return mesh;
        }
    }
}
