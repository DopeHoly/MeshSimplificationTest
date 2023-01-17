using g3;
using gs;
//using Net3dBool;
//using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using UnityEngine;
using CSGV1;
//using CGALDotNet.Geometry;
//using CGALDotNet.Polygons;
//using CGALDotNet;
//using CGALDotNet.Processing;
//using CGALDotNet.Polyhedra;

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

    public class BooleanToolV2 : IBooleanEngine
    {
        public DMesh3 Difference(DMesh3 a, DMesh3 b)
        {
            var meshBoolean = new MeshBoolean();
            var result = new DMesh3(bWantTriGroups: true);
            meshBoolean.Target = a;
            meshBoolean.Tool = b;
            meshBoolean.Result = result;
            meshBoolean.Compute();
            return meshBoolean.Result;
        }

        public DMesh3 Intersection(DMesh3 a, DMesh3 b)
        {
            throw new NotImplementedException();
        }

        public DMesh3 Union(DMesh3 a, DMesh3 b)
        {
            throw new NotImplementedException();
        }
    }

    //public class BooleanToolCSG : IBooleanEngine
    //{
    //    public DMesh3 Difference(DMesh3 a, DMesh3 b)
    //    {
    //        //var meshBoolean = new MeshBoolean();
    //        //var result = new DMesh3(bWantTriGroups: true);
    //        //meshBoolean.Target = a;
    //        //meshBoolean.Tool = b;
    //        //meshBoolean.Result = result;
    //        //meshBoolean.Compute();
    //        //return meshBoolean.Result;
    //        var mesha = CreateMeshGO("object a", a, Colorf.Red);
    //        var meshb = CreateMeshGO("object a", b, Colorf.Red);
    //        var opresult = CSG.Subtract(mesha, meshb);
    //        var result = UnityMeshToDMesh(opresult.mesh);
    //        return result;

    //    }

    //    public DMesh3 Intersection(DMesh3 a, DMesh3 b)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public DMesh3 Union(DMesh3 a, DMesh3 b)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public static GameObject CreateMeshGO(string name, DMesh3 mesh, Material setMaterial = null, bool bCollider = true)
    //    {
    //        var gameObj = new GameObject(name);
    //        gameObj.AddComponent<MeshFilter>();
    //        SetGOMesh(gameObj, mesh);
    //        if (bCollider)
    //        {
    //            gameObj.AddComponent(typeof(MeshCollider));
    //            gameObj.GetComponent<MeshCollider>().enabled = false;
    //        }
    //        if (setMaterial)
    //        {
    //            gameObj.AddComponent<MeshRenderer>().material = setMaterial;
    //        }
    //        else
    //        {
    //            gameObj.AddComponent<MeshRenderer>().material = StandardMaterial(Color.red);
    //        }
    //        return gameObj;
    //    }
    //    public static GameObject CreateMeshGO(string name, DMesh3 mesh, Colorf color, bool bCollider = true)
    //    {
    //        return CreateMeshGO(name, mesh, StandardMaterial(new Color(color.r, color.g, color.b, color.a)), bCollider);
    //    }


    //    public static void SetGOMesh(GameObject go, DMesh3 mesh)
    //    {
    //        DMesh3 useMesh = mesh;
    //        if (!mesh.IsCompact)
    //        {
    //            useMesh = new DMesh3(mesh, true);
    //        }


    //        MeshFilter filter = go.GetComponent<MeshFilter>();
    //        if (filter == null)
    //            throw new Exception("g3UnityUtil.SetGOMesh: go " + go.name + " has no MeshFilter");
    //        Mesh unityMesh = DMeshToUnityMesh(useMesh);
    //        filter.sharedMesh = unityMesh;
    //    }




    //    /// <summary>
    //    /// Convert DMesh3 to unity Mesh
    //    /// </summary>
    //    public static Mesh DMeshToUnityMesh(DMesh3 m, bool bLimitTo64k = false)
    //    {
    //        if (bLimitTo64k && (m.MaxVertexID > 65535 || m.MaxTriangleID > 65535))
    //        {
    //            Debug.Log("g3UnityUtils.DMeshToUnityMesh: attempted to convert DMesh larger than 65535 verts/tris, not supported by Unity!");
    //            return null;
    //        }

    //        Mesh unityMesh = new Mesh();
    //        unityMesh.vertices = dvector_to_vector3(m.VerticesBuffer);
    //        if (m.HasVertexNormals)
    //            unityMesh.normals = (m.HasVertexNormals) ? dvector_to_vector3(m.NormalsBuffer) : null;
    //        if (m.HasVertexColors)
    //            unityMesh.colors = dvector_to_color(m.ColorsBuffer);
    //        if (m.HasVertexUVs)
    //            unityMesh.uv = dvector_to_vector2(m.UVBuffer);
    //        unityMesh.triangles = dvector_to_int(m.TrianglesBuffer);

    //        if (m.HasVertexNormals == false)
    //            unityMesh.RecalculateNormals();

    //        return unityMesh;
    //    }


    //    /// <summary>
    //    /// Convert unity Mesh to a g3.DMesh3. Ignores UV's.
    //    /// </summary>
    //    public static DMesh3 UnityMeshToDMesh(Mesh mesh)
    //    {
    //        Vector3[] mesh_vertices = mesh.vertices;
    //        Vector3f[] dmesh_vertices = new Vector3f[mesh_vertices.Length];
    //        for (int i = 0; i < mesh.vertexCount; ++i)
    //            dmesh_vertices[i] = new Vector3f(mesh_vertices[i].x, mesh_vertices[i].y, mesh_vertices[i].z);

    //        Vector3[] mesh_normals = mesh.normals;
    //        if (mesh_normals != null)
    //        {
    //            Vector3f[] dmesh_normals = new Vector3f[mesh_vertices.Length];
    //            for (int i = 0; i < mesh.vertexCount; ++i)
    //                dmesh_normals[i] = new Vector3f(mesh_normals[i].x, mesh_normals[i].y, mesh_normals[i].z);

    //            return DMesh3Builder.Build(dmesh_vertices, mesh.triangles, dmesh_normals);

    //        }
    //        else
    //        {
    //            return DMesh3Builder.Build<Vector3f, int, Vector3f>(dmesh_vertices, mesh.triangles, null, null);
    //        }
    //    }



    //    public static Material StandardMaterial(Color color)
    //    {
    //        Material mat = new Material(Shader.Find("Standard"));
    //        mat.color = color;
    //        return mat;
    //    }


    //    public static Material SafeLoadMaterial(string sPath)
    //    {
    //        Material mat = null;
    //        try
    //        {
    //            Material loaded = Resources.Load<Material>(sPath);
    //            mat = new Material(loaded);
    //        }
    //        catch (Exception e)
    //        {
    //            Debug.Log("g3UnityUtil.SafeLoadMaterial: exception: " + e.Message);
    //            mat = new Material(Shader.Find("Standard"));
    //            mat.color = Color.red;
    //        }
    //        return mat;
    //    }






    //    // per-type conversion functions
    //    public static Vector3[] dvector_to_vector3(DVector<double> vec)
    //    {
    //        int nLen = vec.Length / 3;
    //        Vector3[] result = new Vector3[nLen];
    //        for (int i = 0; i < nLen; ++i)
    //        {
    //            result[i].x = (float)vec[3 * i];
    //            result[i].y = (float)vec[3 * i + 1];
    //            result[i].z = (float)vec[3 * i + 2];
    //        }
    //        return result;
    //    }
    //    public static Vector3[] dvector_to_vector3(DVector<float> vec)
    //    {
    //        int nLen = vec.Length / 3;
    //        Vector3[] result = new Vector3[nLen];
    //        for (int i = 0; i < nLen; ++i)
    //        {
    //            result[i].x = vec[3 * i];
    //            result[i].y = vec[3 * i + 1];
    //            result[i].z = vec[3 * i + 2];
    //        }
    //        return result;
    //    }
    //    public static Vector2[] dvector_to_vector2(DVector<float> vec)
    //    {
    //        int nLen = vec.Length / 2;
    //        Vector2[] result = new Vector2[nLen];
    //        for (int i = 0; i < nLen; ++i)
    //        {
    //            result[i].x = vec[2 * i];
    //            result[i].y = vec[2 * i + 1];
    //        }
    //        return result;
    //    }
    //    public static Color[] dvector_to_color(DVector<float> vec)
    //    {
    //        int nLen = vec.Length / 3;
    //        Color[] result = new Color[nLen];
    //        for (int i = 0; i < nLen; ++i)
    //        {
    //            result[i].r = vec[3 * i];
    //            result[i].g = vec[3 * i + 1];
    //            result[i].b = vec[3 * i + 2];
    //        }
    //        return result;
    //    }
    //    public static int[] dvector_to_int(DVector<int> vec)
    //    {
    //        // todo this could be faster because we can directly copy chunks...
    //        int nLen = vec.Length;
    //        int[] result = new int[nLen];
    //        for (int i = 0; i < nLen; ++i)
    //            result[i] = vec[i];
    //        return result;
    //    }
    //}

    

    //public class BooleanToolCGAL : IBooleanEngine
    //{
    //    public DMesh3 Difference(DMesh3 a, DMesh3 b)
    //    {
    //        PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
    //        Polyhedron3<EEK> meshA = Convert(a);
    //        Polyhedron3<EEK> meshB = Convert(b);
    //        Polyhedron3<EEK> result = new Polyhedron3<EEK>();

    //        polygon.Difference(meshA, meshB, out result);
    //        return ConvertBack(result);
    //    }

    //    public DMesh3 Intersection(DMesh3 a, DMesh3 b)
    //    {
    //        PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
    //        Polyhedron3<EEK> meshA = Convert(a);
    //        Polyhedron3<EEK> meshB = Convert(b);
    //        Polyhedron3<EEK> result = new Polyhedron3<EEK>();

    //        polygon.Intersection(meshA, meshB, out result);
    //        return ConvertBack(result);
    //    }

    //    public DMesh3 Union(DMesh3 a, DMesh3 b)
    //    {
    //        PolygonMeshProcessingBoolean<EEK> polygon = new PolygonMeshProcessingBoolean<EEK>();
    //        Polyhedron3<EEK> meshA = Convert(a);
    //        Polyhedron3<EEK> meshB = Convert(b);
    //        Polyhedron3<EEK> result = new Polyhedron3<EEK>();

    //        polygon.Union(meshA, meshB, out result);
    //        return ConvertBack(result);
    //    }

    //    private Polyhedron3<EEK> Convert(DMesh3 a)
    //    {
    //        Polyhedron3<EEK> meshA = new Polyhedron3<EEK>();
    //        var pointsA = getPointList(a);
    //        var triangleA = getTriangleIdList(a);
    //        meshA.CreateTriangleMesh(pointsA.ToArray(), pointsA.Count, triangleA.ToArray(), triangleA.Count);
    //        return meshA;
    //    }

    //    private DMesh3 ConvertBack(Polyhedron3<EEK> a)
    //    {
    //        Point3d[] points = { };
    //        int pointsCount = -1;
    //        a.GetPoints(points, pointsCount);
    //        int[] triangleInx = { };
    //        int triangleCnt = -1;
    //        a.GetTriangleIndices(triangleInx, triangleCnt);

    //        //return DMesh3Builder.Build<g3.Vector3d, Index3i, Vector3f>(
    //        //    mesh.Positions.Select(p => new g3.Vector3d(p.X, p.Y, p.Z)),
    //        //    indexedTriangles.Select(t => new Index3i(t.A, t.B, t.C)),
    //        //    TriGroups: mesh.Faces
    //        //    );
    //        return null;
    //    }

    //    private List<Point3d> getPointList(DMesh3 mesh)
    //    {
    //        var points = new List<Point3d>(mesh.VertexCount);

    //        foreach (var vector in mesh.Vertices())
    //        {
    //            points.Add(new Point3d(vector.x, vector.y, vector.z));
    //        }

    //        return points;
    //    }
    //    private List<int> getTriangleIdList(DMesh3 mesh)
    //    {
    //        var triangleIndex = new List<int>(mesh.TriangleCount * 3);

    //        foreach (int triangleid in mesh.TriangleIndices())
    //        {
    //            var triangle = mesh.GetTriangle(triangleid);
    //            //if (triangle.a >= points.Count ||
    //            //     triangle.b >= points.Count ||
    //            //     triangle.c >= points.Count)
    //            //{
    //            //    continue;
    //            //}
    //            triangleIndex.Add(triangle.a);
    //            triangleIndex.Add(triangle.b);
    //            triangleIndex.Add(triangle.c);
    //        }

    //        return triangleIndex;
    //    }
    //}

    //public class BooleanToolNet3dBool : IBooleanEngine
    //{
    //    public DMesh3 Difference(DMesh3 a, DMesh3 b)
    //    {
    //        var meshA = Convert(a);
    //        var meshB = Convert(b);
    //        var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
    //        var tmp = modeller.GetDifference();
    //        return ConvertBack(tmp);
    //    }

    //    public DMesh3 Intersection(DMesh3 a, DMesh3 b)
    //    {
    //        var meshA = Convert(a);
    //        var meshB = Convert(b);
    //        var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
    //        var tmp = modeller.GetIntersection();
    //        return ConvertBack(tmp);
    //    }

    //    public DMesh3 Union(DMesh3 a, DMesh3 b)
    //    {
    //        var meshA = Convert(a);
    //        var meshB = Convert(b);
    //        var modeller = new Net3dBool.BooleanModeller(meshA, meshB);
    //        var tmp = modeller.GetUnion();
    //        return ConvertBack(tmp);
    //    }

    //    private Solid Convert(DMesh3 a)
    //    {
    //        var pointsA = getPointList(a);
    //        var triangleA = getTriangleIdList(a);
    //        return new Solid(pointsA.ToArray(), triangleA.ToArray());
    //    }

    //    private DMesh3 ConvertBack(Solid mesh)
    //    {
    //        var triangleIDs = mesh.GetIndices();
    //        var triangleIDsList = new List<Index3i>();
    //        for(int i = 0; i < triangleIDs.Length; i += 3)
    //        {
    //            triangleIDsList.Add(new Index3i(
    //                triangleIDs[i + 0],
    //                triangleIDs[i + 1],
    //                triangleIDs[i + 2])
    //                );
    //        }

    //        return DMesh3Builder.Build<g3.Vector3d, Index3i, Vector3f>(
    //            mesh.GetVertices().Select(p => new g3.Vector3d(p.X, p.Y, p.Z)),
    //            triangleIDsList
    //            );
    //    }

    //    private List<OpenTK.Vector3d> getPointList(DMesh3 mesh)
    //    {
    //        var points = new List<OpenTK.Vector3d>(mesh.VertexCount);

    //        foreach (var vector in mesh.Vertices())
    //        {
    //            points.Add(new OpenTK.Vector3d(vector.x, vector.y, vector.z));
    //        }

    //        return points;
    //    }
    //    private List<int> getTriangleIdList(DMesh3 mesh)
    //    {
    //        var triangleIndex = new List<int>(mesh.TriangleCount * 3);

    //        foreach (int triangleid in mesh.TriangleIndices())
    //        {
    //            var triangle = mesh.GetTriangle(triangleid);
    //            //if (triangle.a >= points.Count ||
    //            //     triangle.b >= points.Count ||
    //            //     triangle.c >= points.Count)
    //            //{
    //            //    continue;
    //            //}
    //            triangleIndex.Add(triangle.a);
    //            triangleIndex.Add(triangle.b);
    //            triangleIndex.Add(triangle.c);
    //        }

    //        return triangleIndex;
    //    }
    //}
}
