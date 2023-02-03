using Microsoft.VisualStudio.TestTools.UnitTesting;
using g3;
using System;
using System.IO;
using MeshSimplificationTest.SBRep;
using System.Linq;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class SBRepTests
    {
        private static string SamplesPath = "../../../../samples/";
        private static string Sample_CubePath = SamplesPath + "cubeWithGroupNew.obj";
        private static string Sample_CubeRemeshedPath = SamplesPath + "cubeRemeshed.obj";
        private static string Sample_PipePath = SamplesPath + "pipe.obj";
        private static string Sample_GroundWithInfluencePath = SamplesPath + "удалениеНулевыхУчётГруппы.obj";

        [TestMethod]
        public void GenerateSBRepObject()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_GroundWithInfluencePath);
            var sbRep = SBRepBuilder.Convert(mesh);

            Assert.IsNotNull(sbRep);
            Assert.AreEqual(7, sbRep.Faces.Count());
            Assert.AreEqual(7, sbRep.Loops.Count());
            Assert.AreEqual(13, sbRep.Verges.Count());

            var facesWithInternalLoop = sbRep.GetFaces().Where(x => x.InsideLoops.Count() > 0);
            Assert.AreEqual(1, facesWithInternalLoop.Count());
            var face = facesWithInternalLoop.First();
            Assert.AreEqual(1, face.InsideLoops.Count());
            var lid = face.InsideLoops.First();
            var loop = sbRep.Loops[lid];
            Assert.AreEqual(1, loop.Verges.Count());
        }

        [TestMethod]
        public void PlaneFaceTest()
        {
            ///http://mathprofi.ru/uravnenie_ploskosti.html
            ///Пример 3
            Vector3d v0 = new Vector3d(1, -2, 0);
            Vector3d v1 = new Vector3d(2, 0, -1);
            Vector3d v2 = new Vector3d(0, -1, 2);
            var plane = SBRepBuilder.PlaneFace.FromPoints(v0, v1, v2, Vector3d.Zero);
            Assert.AreEqual(5, plane.A);
            Assert.AreEqual(-1, plane.B);
            Assert.AreEqual(3, plane.C);
            Assert.AreEqual(-7, plane.D);
        }

        [TestMethod]
        public void CubeConvertTest()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_CubePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            Assert.IsNotNull(sbRep);
            Assert.AreEqual(8, sbRep.Vertices.Count);
            Assert.AreEqual(12, sbRep.Edges.Count);
            Assert.AreEqual(12, sbRep.Verges.Count);
            Assert.AreEqual(6, sbRep.Loops.Count);
            Assert.AreEqual(6, sbRep.Faces.Count);
        }

        [TestMethod]
        public void GetClosetContour()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_CubePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            var contour = sbRep.GetClosedContour(0);
        }

        [TestMethod]
        public void GetAreaTest()
        {
            var points = new List<Vector2d>();
            points.Add(new Vector2d(3,4));
            points.Add(new Vector2d(5,11));
            points.Add(new Vector2d(12,8));
            points.Add(new Vector2d(9,5));
            points.Add(new Vector2d(5,6));
            var area = SBRepObject.GetArea(points);
            Assert.AreEqual(30.0, area);
        }

        [TestMethod]
        public void GetAreaLoopTest()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_CubePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            var loopsAreas = new List<double>();
            foreach(var loop in sbRep.Loops)
            {
                loopsAreas.Add(sbRep.GetLoopArea(loop.ID));
            }
            Assert.IsNotNull(loopsAreas);
            Assert.IsTrue(
                loopsAreas.All(
                    x => Math.Abs(x - loopsAreas.First()) < 1e-9 
                    )
                );
        }
    }


}
