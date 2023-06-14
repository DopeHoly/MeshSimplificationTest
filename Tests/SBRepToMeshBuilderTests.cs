using g3;
using SBRep;
using SBRep.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Tests
{
    [TestClass]
    public class SBRepToMeshBuilderTests
    {
        private static string SamplesPath = "../../../../samples/";
        private static string Sample_Cube4x4Path = SamplesPath + "Кубик4Fix.obj";

        public void CheckDoubleEqualsWithEPS(double a, double b, double eps = 1e-5)
        {
            var dif = Math.Abs(a - b);
            if(dif > eps)
                Assert.AreEqual(a, b);
        }

        public void CheckVector3dEqualsWithEps(Vector3d a, Vector3d b, double eps = 1e-5)
        {
            CheckDoubleEqualsWithEPS(a.x, b.x, eps);
            CheckDoubleEqualsWithEPS(a.y, b.y, eps);
            CheckDoubleEqualsWithEPS(a.z, b.z, eps);
        }

        [TestMethod]
        public void Call_CDT()
        {
            var vertices = new List<g3.Vector2d>();
            var edges = new List<g3.Index2i>();

            vertices.Add(new g3.Vector2d(2.0, 2.0));
            vertices.Add(new g3.Vector2d(2.0, -2.0));
            vertices.Add(new g3.Vector2d(-2.0, -2.0));
            vertices.Add(new g3.Vector2d(-2.0, 2.0));

            vertices.Add(new g3.Vector2d(1.0, 1.0));
            vertices.Add(new g3.Vector2d(1.0, -1.0));
            vertices.Add(new g3.Vector2d(-1.0, -1.0));
            vertices.Add(new g3.Vector2d(-1.0, 1.0));

            edges.Add(new g3.Index2i(0, 1));
            edges.Add(new g3.Index2i(1, 2));
            edges.Add(new g3.Index2i(2, 3));
            edges.Add(new g3.Index2i(3, 0));

            edges.Add(new g3.Index2i(4, 5));
            edges.Add(new g3.Index2i(5, 6));
            edges.Add(new g3.Index2i(6, 7));
            edges.Add(new g3.Index2i(7, 4));

            IEnumerable<g3.Vector2d> triVertices = null;
            IEnumerable<g3.Index3i> triangles = null;

            var noError = TriangulationCaller.CDT(
                vertices, edges, 
                out triVertices, out triangles);

            Assert.IsTrue(noError);
            Assert.AreEqual(8, triVertices.Count());
            Assert.AreEqual(8, triangles.Count());
        }

        [TestMethod]
        public void CheckRotateMatrix()
        {
            var points = new List<Vector3d>()
            {
                new Vector3d(0.22441116, 0.63020881, 0.20501059),
                new Vector3d(0.45583489, 0.4607472 , 0.84695864),
                new Vector3d(0.06333853, 0.65616212, 0.84673437)
            };
            var normal = MathUtil.Normal(points[0], points[1], points[2]);

            var transformMatrix = SBRepToMeshBuilder.CalculateTransform(points, normal);
            var mtx = transformMatrix.Item1;

            CheckDoubleEqualsWithEPS(-0.03362031,mtx[0, 0]);
            CheckDoubleEqualsWithEPS(-0.06753388, mtx[0, 1]);
            CheckDoubleEqualsWithEPS(0.99715036, mtx[0, 2]);

            CheckDoubleEqualsWithEPS(-0.89520333, mtx[1, 0]);
            CheckDoubleEqualsWithEPS(0.44565794, mtx[1, 1]);
            CheckDoubleEqualsWithEPS(0,mtx[1, 2]);

            CheckDoubleEqualsWithEPS(-0.44438798, mtx[2, 0]);
            CheckDoubleEqualsWithEPS(-0.89265233, mtx[2, 1]);
            CheckDoubleEqualsWithEPS(-0.07543971, mtx[3, 2]);

            var vertice2D = SBRepToMeshBuilder.ConvertTo2D(points, transformMatrix.Item1, transformMatrix.Item3);
            var vertice3D = SBRepToMeshBuilder.RevertTo3D(vertice2D, transformMatrix.Item2, transformMatrix.Item3);

            for(int i = 0; i < points.Count; i++)
            {
                CheckVector3dEqualsWithEps(points[i], vertice3D.ElementAt(i));
            }
        }

        [TestMethod]
        public void CheckSlowAndFastConvertToMesh()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_Cube4x4Path);
            var sbRep = SBRepBuilder.Convert(mesh);
            var resultSlow = SBRepToMeshBuilder.Convert(sbRep);
            var resultFast = SBRepToMeshBuilder.ConvertParallel(sbRep);

            Assert.AreEqual(resultSlow.VertexCount, resultFast.VertexCount);
            Assert.AreEqual(resultSlow.EdgeCount, resultFast.EdgeCount);
            
        }
    }
}
