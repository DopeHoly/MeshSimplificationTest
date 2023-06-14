using g3;
using gs;
using SBRep;
using SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static SBRep.SBRepToMeshBuilderV2;

namespace GeometryLib
{
    public class PlanesStapler
    {
        public static IEnumerable<int> GetBoundaryEdges(DMesh3 mesh)
        {
            return mesh.EdgeIndices().Where(x => {
                var edgeT = mesh.GetEdgeT(x);
                return edgeT.a == -1 || edgeT.b == -1;
                }).ToList();
        }

        public static IEnumerable<int> GetUniqueVerticesIDFromEdgeIDs(DMesh3 mesh, IEnumerable<int> edges)
        {
            return edges.SelectMany(x =>
            {
                var edgeV = mesh.GetEdgeV(x);
                return new[]{ edgeV.a, edgeV.b};
            }).Distinct().ToList();
        }

        public static IEnumerable<int> GetBoundaryVertices(DMesh3 mesh)
        {
            return GetUniqueVerticesIDFromEdgeIDs(mesh, GetBoundaryEdges(mesh));
        }

        public static Dictionary<int, Vector3d> GetVerticesCoordinate(DMesh3 mesh, IEnumerable<int> vids)
        {
            var dict = new Dictionary<int, Vector3d>();
            foreach (var v in vids)
            {
                dict.Add(v, mesh.GetVertex(v));
            }
            return dict;
        }
        public static T[] GetColumn<T>(T[,] matrix, int columnNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(0))
                    .Select(x => matrix[x, columnNumber])
                    .ToArray();
        }

        public static T[] GetRow<T>(T[,] matrix, int rowNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(1))
                    .Select(x => matrix[rowNumber, x])
                    .ToArray();
        }

        public static IEnumerable<Index2i> GetBindVertices(DMesh3 meshA, DMesh3 meshB)
        {
            var boundaryVerticesA = GetBoundaryVertices(meshA);
            var boundaryVerticesB = GetBoundaryVertices(meshB);

            var boundaryVCoordA = GetVerticesCoordinate(meshA, boundaryVerticesA);
            var boundaryVCoordB = GetVerticesCoordinate(meshB, boundaryVerticesB);

            var vtxACount = boundaryVerticesA.Count();
            var vtxBCount = boundaryVerticesB.Count();

            var distanceMtx = new double[vtxACount, vtxBCount];

            int aIndex = 0; int bIndex = 0;

            foreach (var vtxA in boundaryVCoordA)
            {
                bIndex = 0;
                foreach (var vtxB in boundaryVCoordB)
                {
                    distanceMtx[aIndex, bIndex] = (vtxA.Value.xy - vtxB.Value.xy).LengthSquared;
                    ++bIndex;
                }
                ++aIndex;
            }

            var connection = new List<Index2i>();


            aIndex = 0;
            foreach (var vtxA in boundaryVCoordA)
            {
                //var counter = 0;
                //var sorted = GetRow(distanceMtx, aIndex).Select(x => new Tuple<int, double>(counter++, x)).ToList();
                //sorted.Sort(x => x.Item2);


                var minValue = double.MaxValue;
                var minIndex = -1;
                for (int i = 0; i < vtxBCount; ++i)
                {
                    var value = distanceMtx[aIndex, i];
                    if (value < minValue)
                    {
                        minIndex = i;
                        minValue = value;
                    }
                }
                Debug.Assert(minIndex != -1);

                connection.Add(new Index2i(vtxA.Key, boundaryVCoordB.ElementAt(minIndex).Key));

                ++aIndex;
            }

            bIndex = 0;
            foreach (var vtxB in boundaryVCoordB)
            {
                var minValue = double.MaxValue;
                var minIndex = -1;
                for (int i = 0; i < vtxACount; ++i)
                {
                    var value = distanceMtx[i, bIndex];
                    if (value < minValue)
                    {
                        minIndex = i;
                        minValue = value;
                    }
                }
                Debug.Assert(minIndex != -1);

                connection.Add(new Index2i(boundaryVCoordA.ElementAt(minIndex).Key, vtxB.Key));

                ++bIndex;
            }
            connection = connection.Distinct().ToList();

            return connection;
        }

        public static DMesh3 StaplerTopBottomPlanes(DMesh3 meshA, DMesh3 meshB, double eps = 1e-6)
        {
            var binds = PlanesStapler.GetBindVertices(meshA, meshB);

            var replaceDict = new List<Index2i>();
            var sqEps = eps * eps;

            //поиск прилегающих точек
            foreach (var bind in binds)
            {
                var vtxA = meshA.GetVertex(bind.a);
                var vtxB = meshB.GetVertex(bind.b);
                var distance = (vtxA - vtxB).LengthSquared;
                if (distance < sqEps)
                    replaceDict.Add(bind);
            }

            return null;
        }

        public static DMesh3 TestStapler(DMesh3 meshA, DMesh3 meshB, double eps = 1e-6)
        {
            var boundaryEdgesA = GetBoundaryEdges(meshA);
            var boundaryEdgesB = GetBoundaryEdges(meshB);


            var binds = PlanesStapler.GetBindVertices(meshA, meshB);

            var replaceDict = new Dictionary<int, int>();
            var correctBind = new List<Index2i>();
            var correctBindADict = new Dictionary<int, List<int>>();
            //var correctBindBDict = new Dictionary<int, int>();
            var sqEps = eps * eps;

            //поиск прилегающих точек
            foreach (var bind in binds)
            {
                var vtxA = meshA.GetVertex(bind.a);
                var vtxB = meshB.GetVertex(bind.b);
                var distance = (vtxA - vtxB).LengthSquared;
                if (distance < sqEps)
                    replaceDict.Add(bind.b, bind.a);
                else
                {
                    correctBind.Add(bind);
                    if (!correctBindADict.ContainsKey(bind.a))
                        correctBindADict.Add(bind.a, new List<int>());
                    correctBindADict[bind.a].Add(bind.b);
                    //correctBindBDict.Add(bind.b, bind.a);
                }
            }

            //объединение списка вершин результирующего объекта
            var vertices = new List<Vector3d>();

            var aVerticeRD = new Dictionary<int, int>();
            foreach(var vid in meshA.VertexIndices())
            {
                var vtx = meshA.GetVertex(vid);
                aVerticeRD.Add(vid, vertices.Count);
                vertices.Add(new Vector3d(vtx.x, vtx.y, vtx.z + 100));
            }

            var bVerticeRD = new Dictionary<int, int>();
            foreach(var vid in meshB.VertexIndices())
            {
                if (replaceDict.ContainsKey(vid))
                {
                    bVerticeRD.Add(vid, aVerticeRD[replaceDict[vid]]);
                    continue;
                }
                var vtx = meshB.GetVertex(vid);
                bVerticeRD.Add(vid, vertices.Count);
                vertices.Add(vtx);
            }
            var firstVtx = vertices[0];
            vertices = vertices.Select(vtx => (vtx -  firstVtx) * 0.01).ToList();

            //индексация треугольников

            var triangles = new List<Index3i>();
            var faces = new List<int>();
            //из сетки meshA
            foreach (var tid in meshA.TriangleIndices())
            {
                var tri = meshA.GetTriangle(tid);
                triangles.Add(new Index3i()
                {
                    a = aVerticeRD[tri.a],
                    b = aVerticeRD[tri.b],
                    c = aVerticeRD[tri.c],
                });
                faces.Add(1);
            }

            //из сетки meshB
            foreach (var tid in meshB.TriangleIndices())
            {
                var tri = meshB.GetTriangle(tid);
                triangles.Add(new Index3i()
                {
                    a = bVerticeRD[tri.a],
                    b = bVerticeRD[tri.b],
                    c = bVerticeRD[tri.c],
                });
                faces.Add(2);
            }

            //объединение сеток
            foreach (var eid in boundaryEdgesA)
            {
                var edgeVerticeIDs = meshA.GetEdgeV(eid);

                var faceVertices = new List<int>();
                faceVertices.Add(aVerticeRD[edgeVerticeIDs.a]);
                faceVertices.Add(aVerticeRD[edgeVerticeIDs.b]);

                if (correctBindADict.ContainsKey(edgeVerticeIDs.a))
                {
                    var bind = correctBindADict[edgeVerticeIDs.a];
                    foreach (var vid in bind)
                        faceVertices.Add(bVerticeRD[vid]);
                }
                if (correctBindADict.ContainsKey(edgeVerticeIDs.b))
                {
                    var bind = correctBindADict[edgeVerticeIDs.b];
                    foreach (var vid in bind)
                        faceVertices.Add(bVerticeRD[vid]);
                }
                faceVertices = faceVertices.Distinct().ToList();

                switch (faceVertices.Count())
                {
                    case 2: continue;
                    case 3:
                        triangles.Add(new Index3i()
                        {
                            a = faceVertices[0],
                            b = faceVertices[1],
                            c = faceVertices[2],
                        });
                        faces.Add(3);
                        continue;
                    case 4:
                        triangles.Add(new Index3i()
                        {
                            a = faceVertices[0],
                            b = faceVertices[1],
                            c = faceVertices[2],
                        });
                        faces.Add(3);
                        triangles.Add(new Index3i()
                        {
                            a = faceVertices[1],
                            b = faceVertices[2],
                            c = faceVertices[3],
                        });
                        faces.Add(3);
                        continue;
                    default:
                        //triangles.Add(new Index3i()
                        //{
                        //    a = faceVertices[0],
                        //    b = faceVertices[1],
                        //    c = faceVertices[2],
                        //});
                        //faces.Add(3);
                        //triangles.Add(new Index3i()
                        //{
                        //    a = faceVertices[1],
                        //    b = faceVertices[2],
                        //    c = faceVertices[3],
                        //});
                        //faces.Add(3);
                        continue;
                }
            }

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                vertices,
                triangles,
                TriGroups: faces
                );

            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();
            return mesh;
        }

        public static SBRepObject SbrepTestStapler(DMesh3 meshA, DMesh3 meshB, double eps = 1e-6)
        {
            if (meshA == null || meshB == null)
                return new SBRepObject();

            var sbRepObject = new SBRepObject();

            var boundaryEdgesA = GetBoundaryEdges(meshA);
            var boundaryEdgesB = GetBoundaryEdges(meshB);

            var binds = PlanesStapler.GetBindVertices(meshA, meshB);

            var replaceDict = new Dictionary<int, int>();
            var correctBind = new List<Index2i>();
            var correctBindADict = new Dictionary<int, List<int>>();
            //var correctBindBDict = new Dictionary<int, int>();
            var sqEps = eps * eps;

            //поиск прилегающих точек
            foreach (var bind in binds)
            {
                var vtxA = meshA.GetVertex(bind.a);
                var vtxB = meshB.GetVertex(bind.b);
                var distance = (vtxA - vtxB).LengthSquared;
                if (distance < sqEps)
                    replaceDict.Add(bind.b, bind.a);
                else
                {
                    correctBind.Add(bind);
                    if (!correctBindADict.ContainsKey(bind.a))
                        correctBindADict.Add(bind.a, new List<int>());
                    correctBindADict[bind.a].Add(bind.b);
                    //correctBindBDict.Add(bind.b, bind.a);
                }
            }

            //объединение списка вершин результирующего объекта
            var vertices = new List<Vector3d>();

            var aVerticeRD = new Dictionary<int, int>();
            foreach (var vid in meshA.VertexIndices())
            {
                var vtx = meshA.GetVertex(vid);
                aVerticeRD.Add(vid, vertices.Count);
                vertices.Add(new Vector3d(vtx.x, vtx.y, vtx.z + 100));
            }

            var bVerticeRD = new Dictionary<int, int>();
            foreach (var vid in meshB.VertexIndices())
            {
                if (replaceDict.ContainsKey(vid))
                {
                    bVerticeRD.Add(vid, aVerticeRD[replaceDict[vid]]);
                    continue;
                }
                var vtx = meshB.GetVertex(vid);
                bVerticeRD.Add(vid, vertices.Count);
                vertices.Add(vtx);
            }
            var firstVtx = vertices[0];
            vertices = vertices.Select(vtx => (vtx - firstVtx) * 0.01).ToList();

            foreach (var vtx in vertices)
            {
                //var coord = mesh.GetVertex(vid);
                sbRepObject.Vertices.Add(new SBRep_Vtx()
                {
                    //ID = vid,
                    Coordinate = vtx,
                });
            }

            foreach (var eid in meshA.EdgeIndices())
            {
                if (!boundaryEdgesA.Contains(eid))
                    continue;
                var verticesAB = meshA.GetEdgeV(eid);
                sbRepObject.Edges.Add(new SBRep_Edge()
                {
                    //ID = eid,
                    Vertices = new Index2i(aVerticeRD[verticesAB.a], aVerticeRD[verticesAB.b]),
                });
            }
            foreach (var eid in meshB.EdgeIndices())
            {
                if (!boundaryEdgesB.Contains(eid))
                    continue;
                var verticesAB = meshB.GetEdgeV(eid);
                sbRepObject.Edges.Add(new SBRep_Edge()
                {
                    //ID = eid,
                    Vertices = new Index2i(bVerticeRD[verticesAB.a], bVerticeRD[verticesAB.b]),
                });
            }

            foreach (var eid in binds)
            {
                sbRepObject.Edges.Add(new SBRep_Edge()
                {
                    //ID = eid,
                    Vertices = new Index2i(aVerticeRD[eid.a], bVerticeRD[eid.b]),
                });
            }


            foreach (var edge in sbRepObject.Edges)
            {
                sbRepObject.Verges.Add(new SBRep_Verge()
                {
                    ID = edge.ID,
                    Edges = new List<int> { edge.ID },
                });
            }


            //Индексирует обратные связи между разными объектами
            sbRepObject.RedefineFeedbacks();
            return sbRepObject;
        }

        public static DMesh3 Stapler(DMesh3 meshA, DMesh3 meshB, double eps = 1e-6,
            Action<IEnumerable<Vector2d>, IEnumerable<Index2i>> visualizer = null,
            Action<IEnumerable<Vector2d>, IEnumerable<Index3i>> triVisualizer = null)
        {
            var boundaryEdgesA = GetBoundaryEdges(meshA);
            var boundaryEdgesB = GetBoundaryEdges(meshB);

            var verticesA = GetUniqueVerticesIDFromEdgeIDs(meshA, boundaryEdgesA);
            var verticesB = GetUniqueVerticesIDFromEdgeIDs(meshB, boundaryEdgesB);

            var binds = PlanesStapler.GetBindVertices(meshA, meshB);

            var replaceDict = new Dictionary<int, int>();
            //var correctBind = new List<Index2i>();
            //var correctBindADict = new Dictionary<int, List<int>>();
            //var correctBindBDict = new Dictionary<int, int>();
            var sqEps = eps * eps;

            //поиск прилегающих точек
            foreach (var bind in binds)
            {
                var vtxA = meshA.GetVertex(bind.a);
                var vtxB = meshB.GetVertex(bind.b);
                var distance = (vtxA - vtxB).LengthSquared;
                if (distance < sqEps)
                    replaceDict.Add(bind.b, bind.a);
                //else
                //{
                //    correctBind.Add(bind);
                //    if (!correctBindADict.ContainsKey(bind.a))
                //        correctBindADict.Add(bind.a, new List<int>());
                //    correctBindADict[bind.a].Add(bind.b);
                //    //correctBindBDict.Add(bind.b, bind.a);
                //}
            }

            //объединение списка вершин результирующего объекта
            var vertices = new List<Vector3d>();
            var vertices2d = new List<Vector2d>();
            var center = Vector2d.Zero;            
            foreach (var vid in meshA.VertexIndices())
            {
                var vtx = meshA.GetVertex(vid);
                center += vtx.xy;
            }            
            foreach (var vid in meshB.VertexIndices())
            {
                var vtx = meshA.GetVertex(vid);
                center += vtx.xy;
            }
            center /= (double)(meshA.VertexCount +  meshB.VertexCount);
                        
            var aVerticeRD = new Dictionary<int, int>();
            foreach (var vid in meshA.VertexIndices())
            {
                var vtx = meshA.GetVertex(vid);
                aVerticeRD.Add(vid, vertices.Count);
                vertices.Add(new Vector3d(vtx.x, vtx.y, vtx.z + 100));
                //vertices.Add(new Vector3d(vtx.x, vtx.y, vtx.z));
                //vertices2d.Add(vtx.xy - center);
            }

            var bVerticeRD = new Dictionary<int, int>();
            foreach (var vid in meshB.VertexIndices())
            {
                if (replaceDict.ContainsKey(vid))
                {
                    bVerticeRD.Add(vid, aVerticeRD[replaceDict[vid]]);
                    continue;
                }
                var vtx = meshB.GetVertex(vid);
                bVerticeRD.Add(vid, vertices.Count);
                vertices.Add(vtx);
                //vertices2d.Add((vtx.xy - center) * 2);
            }

            var triangleRD = new Dictionary<int, int>();
            var triangleReverseARD = new Dictionary<int, int>();
            var triangleReverseBRD = new Dictionary<int, int>();
            var triangleARD = new Dictionary<int, int>();
            //var triangleBRD = new Dictionary<int, int>();
            foreach (var vid in verticesA)
            {
                var vtx = meshA.GetVertex(vid);
                triangleRD.Add(vertices2d.Count, aVerticeRD[vid]);
                triangleReverseARD.Add(vid, vertices2d.Count);
                triangleARD.Add(vertices2d.Count, vid);
                vertices2d.Add(vtx.xy - center);
            }
            foreach (var vid in verticesB)
            {
                if (replaceDict.ContainsKey(vid))
                {
                    triangleReverseBRD.Add(vid, triangleReverseARD[replaceDict[vid]]);
                    //triangleReverseBRD.Add(vid, triangleReverseARD[replaceDict[vid]]);
                    continue;
                }
                var vtx = meshB.GetVertex(vid);
                triangleRD.Add(vertices2d.Count, bVerticeRD[vid]);
                triangleReverseBRD.Add(vid, vertices2d.Count);
                //triangleReverseBRD.Add(vid, vertices2d.Count);
                vertices2d.Add((vtx.xy - center) * 1.2);
            }


            var firstVtx = vertices[0];
            vertices = vertices.Select(vtx => (vtx - firstVtx) * 0.01).ToList();

            //fillEdges
            var edges = new List<Index2i>();
            foreach (var eid in boundaryEdgesA)
            {
                var verticesAB = meshA.GetEdgeV(eid);
                edges.Add(new Index2i(triangleReverseARD[verticesAB.a], triangleReverseARD[verticesAB.b]));
            }
            foreach (var eid in boundaryEdgesB)
            {
                var verticesAB = meshB.GetEdgeV(eid);
                edges.Add(new Index2i(triangleReverseBRD[verticesAB.a], triangleReverseBRD[verticesAB.b]));
            }
            edges = edges.Distinct().ToList();

            //CallTriangulation

            IEnumerable<g3.Vector2d> triVertices = null;
            IEnumerable<g3.Index3i> triangulationTri = null;

            if(visualizer != null)
                visualizer(vertices2d, edges);

            var noError = TriangulationCaller.CDT(
                vertices2d, edges,
                out triVertices, out triangulationTri);

            if (triVisualizer != null)
                triVisualizer(vertices2d, triangulationTri);

            var triangles = new List<Index3i>();
            var faces = new List<int>();
            //из сетки meshA
            foreach (var tid in meshA.TriangleIndices())
            {
                var tri = meshA.GetTriangle(tid);
                triangles.Add(new Index3i()
                {
                    a = aVerticeRD[tri.a],
                    b = aVerticeRD[tri.b],
                    c = aVerticeRD[tri.c],
                });
                faces.Add(1);
            }

            //из сетки meshB
            foreach (var tid in meshB.TriangleIndices())
            {
                var tri = meshB.GetTriangle(tid);
                triangles.Add(new Index3i()
                {
                    a = bVerticeRD[tri.a],
                    b = bVerticeRD[tri.b],
                    c = bVerticeRD[tri.c],
                });
                faces.Add(2);
            }

            foreach(var tri in triangulationTri)
            {
                var a = triangleRD[tri.a];
                var b = triangleRD[tri.b];
                var c = triangleRD[tri.c];

                //if (triangleARD.ContainsValue(a) &&
                //    triangleARD.ContainsValue(b) &&
                //    triangleARD.ContainsValue(c))
                //    continue;

                triangles.Add(new Index3i(a, b, c));
                faces.Add(3);
            }

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                vertices,
                triangles,
                TriGroups: faces
                );

            var meshRepairOrientation = new MeshRepairOrientation(mesh);
            meshRepairOrientation.OrientComponents();
            meshRepairOrientation.SolveGlobalOrientation();
            return mesh;
        }
        
    }
}
