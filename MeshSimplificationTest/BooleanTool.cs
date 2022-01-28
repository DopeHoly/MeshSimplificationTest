using g3;
using gs;
using Net3dBool;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CGALDotNet.Geometry;
using CGALDotNet.Polygons;
using CGALDotNet;
using CGALDotNet.Processing;
using CGALDotNet.Polyhedra;

namespace MeshSimplificationTest
{
    public interface IBooleanEngine
    {
        DMesh3 Intersection(DMesh3 a, DMesh3 b);
        DMesh3 Union(DMesh3 a, DMesh3 b);
        DMesh3 Difference(DMesh3 a, DMesh3 b);
    }
    public class BooleanTool : IBooleanEngine
    {
        private static Func<BoundedImplicitFunction3d, int, DMesh3> GenerateMeshF = (root, numcells) => {
            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = root;
            c.RootMode = MarchingCubesPro.RootfindingModes.LerpSteps;      // cube-edge convergence method
            c.RootModeSteps = 5;                                        // number of iterations
            c.Bounds = root.Bounds();
            c.CubeSize = c.Bounds.MaxDim / numcells;
            c.Bounds.Expand(3 * c.CubeSize);                            // leave a buffer of cells
            c.Generate();
            MeshNormals.QuickCompute(c.Mesh);                           // generate normals
            return c.Mesh;
        };

        static Func<DMesh3, int, double, BoundedImplicitFunction3d> MeshToImplicitF = (meshIn, numcells, max_offset) => {
            double meshCellsize = meshIn.CachedBounds.MaxDim / numcells;
            MeshSignedDistanceGrid levelSet = new MeshSignedDistanceGrid(meshIn, meshCellsize);
            levelSet.ExactBandWidth = (int)(max_offset / meshCellsize) + 1;
            levelSet.Compute();
            return new DenseGridTrilinearImplicit(levelSet.Grid, levelSet.GridOrigin, levelSet.CellSize);
        };

        public DMesh3 Op(DMesh3 a, DMesh3 b, Func<BoundedImplicitFunction3d, BoundedImplicitFunction3d, BoundedImplicitFunction3d> creator)
        {
            var cellCount = 524;
            var offset = 0.001;
            try
            {
                // using half res SDF and full res remesh produces best results for me
                var sdfA = MeshToImplicitF(a, cellCount / 2, offset);
                var sdfB = MeshToImplicitF(b, cellCount / 2, offset);

                var diff = creator(sdfA, sdfB);
                var m = GenerateMeshF(diff, cellCount);
                //var remeshTool = new RemeshTool()
                //{
                //    EdgeLength = 1,
                //    Angle = 30
                //};
                //m = remeshTool.Remesh(m,
                //    System.Threading.CancellationToken.None);
                return m;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }

            return null;
        }

        public DMesh3 Intersection(DMesh3 a, DMesh3 b)
        {
            return Op(a, b, (fa, fb) => new ImplicitIntersection3d() { A = fa, B = fb });
        }

        public DMesh3 Difference(DMesh3 a, DMesh3 b)
        {
            return Op(a, b, (fa, fb) => new ImplicitDifference3d() { A = fa, B = fb });
        }

        public DMesh3 Union(DMesh3 a, DMesh3 b)
        {
            return Op(a, b, (fa, fb) => new ImplicitUnion3d() { A = fa, B = fb });
        }
    }

    public class BooleanToolCGAL : IBooleanEngine
    {
        public DMesh3 Difference(DMesh3 a, DMesh3 b)
        {
            PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
            Polyhedron3<EEK> meshA = Convert(a);
            Polyhedron3<EEK> meshB = Convert(b);
            Polyhedron3<EEK> result = new Polyhedron3<EEK>();

            polygon.Difference(meshA, meshB, out result);
            return ConvertBack(result);
        }

        public DMesh3 Intersection(DMesh3 a, DMesh3 b)
        {
            PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
            Polyhedron3<EEK> meshA = Convert(a);
            Polyhedron3<EEK> meshB = Convert(b);
            Polyhedron3<EEK> result = new Polyhedron3<EEK>();

            polygon.Intersection(meshA, meshB, out result);
            return ConvertBack(result);
        }

        public DMesh3 Union(DMesh3 a, DMesh3 b)
        {
            PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
            Polyhedron3<EEK> meshA = Convert(a);
            Polyhedron3<EEK> meshB = Convert(b);
            Polyhedron3<EEK> result = new Polyhedron3<EEK>();

            polygon.Union(meshA, meshB, out result);
            return ConvertBack(result);
        }

        private Polyhedron3<EEK> Convert(DMesh3 a)
        {
            Polyhedron3<EEK> meshA = new Polyhedron3<EEK>();
            var pointsA = getPointList(a);
            var triangleA = getTriangleIdList(a);
            meshA.CreateTriangleMesh(pointsA.ToArray(), pointsA.Count, triangleA.ToArray(), triangleA.Count);
            return meshA;
        }

        private DMesh3 ConvertBack(Polyhedron3<EEK> a)
        {
            Point3d[] points = { };
            int pointsCount = -1;
            a.GetPoints(points, pointsCount);
            int[] triangleInx = { };
            int triangleCnt = -1;
            a.GetTriangleIndices(triangleInx, triangleCnt);

            //return DMesh3Builder.Build<g3.Vector3d, Index3i, Vector3f>(
            //    mesh.Positions.Select(p => new g3.Vector3d(p.X, p.Y, p.Z)),
            //    indexedTriangles.Select(t => new Index3i(t.A, t.B, t.C)),
            //    TriGroups: mesh.Faces
            //    );
            return null;
        }

        private List<Point3d> getPointList(DMesh3 mesh)
        {
            var points = new List<Point3d>(mesh.VertexCount);

            foreach (var vector in mesh.Vertices())
            {
                points.Add(new Point3d(vector.x, vector.y, vector.z));
            }

            return points;
        }
        private List<int> getTriangleIdList(DMesh3 mesh)
        {
            var triangleIndex = new List<int>(mesh.TriangleCount * 3);

            foreach (int triangleid in mesh.TriangleIndices())
            {
                var triangle = mesh.GetTriangle(triangleid);
                //if (triangle.a >= points.Count ||
                //     triangle.b >= points.Count ||
                //     triangle.c >= points.Count)
                //{
                //    continue;
                //}
                triangleIndex.Add(triangle.a);
                triangleIndex.Add(triangle.b);
                triangleIndex.Add(triangle.c);
            }

            return triangleIndex;
        }
    }

    public class BooleanToolNet3dBool : IBooleanEngine
    {
        public DMesh3 Difference(DMesh3 a, DMesh3 b)
        {
            var meshA = Convert(a);
            var meshB = Convert(b);
            var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
            var tmp = modeller.GetDifference();
            return ConvertBack(tmp);
        }

        public DMesh3 Intersection(DMesh3 a, DMesh3 b)
        {
            var meshA = Convert(a);
            var meshB = Convert(b);
            var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
            var tmp = modeller.GetIntersection();
            return ConvertBack(tmp);
        }

        public DMesh3 Union(DMesh3 a, DMesh3 b)
        {
            var meshA = Convert(a);
            var meshB = Convert(b);
            var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
            var tmp = modeller.GetUnion();
            return ConvertBack(tmp);
        }

        private Solid Convert(DMesh3 a)
        {
            var pointsA = getPointList(a);
            var triangleA = getTriangleIdList(a);
            return new Solid(pointsA.ToArray(), triangleA.ToArray());
        }

        private DMesh3 ConvertBack(Solid mesh)
        {
            var triangleIDs = mesh.GetIndices();
            var triangleIDsList = new List<Index3i>();
            for(int i = 0; i < triangleIDs.Length; i += 3)
            {
                triangleIDsList.Add(new Index3i(
                    triangleIDs[i + 0],
                    triangleIDs[i + 1],
                    triangleIDs[i + 2])
                    );
            }

            return DMesh3Builder.Build<g3.Vector3d, Index3i, Vector3f>(
                mesh.GetVertices().Select(p => new g3.Vector3d(p.X, p.Y, p.Z)),
                triangleIDsList
                );
        }

        private List<OpenTK.Vector3d> getPointList(DMesh3 mesh)
        {
            var points = new List<OpenTK.Vector3d>(mesh.VertexCount);

            foreach (var vector in mesh.Vertices())
            {
                points.Add(new OpenTK.Vector3d(vector.x, vector.y, vector.z));
            }

            return points;
        }
        private List<int> getTriangleIdList(DMesh3 mesh)
        {
            var triangleIndex = new List<int>(mesh.TriangleCount * 3);

            foreach (int triangleid in mesh.TriangleIndices())
            {
                var triangle = mesh.GetTriangle(triangleid);
                //if (triangle.a >= points.Count ||
                //     triangle.b >= points.Count ||
                //     triangle.c >= points.Count)
                //{
                //    continue;
                //}
                triangleIndex.Add(triangle.a);
                triangleIndex.Add(triangle.b);
                triangleIndex.Add(triangle.c);
            }

            return triangleIndex;
        }
    }
}
