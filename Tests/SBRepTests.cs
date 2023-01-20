using Microsoft.VisualStudio.TestTools.UnitTesting;
using g3;
using System;
using System.IO;
using MeshSimplificationTest.SBRep;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class SBRepTests
    {
        private static string SamplesPath = "../../../samples/";
        private static string Sample_CubePath = SamplesPath + "cubeWithGroupNew.obj";
        private static string Sample_CubeRemeshedPath = SamplesPath + "cubeRemeshed.obj";
        private static string Sample_PipePath = SamplesPath + "pipe.obj";
        private static string Sample_GroundWithInfluencePath = SamplesPath + "удалениеНулевыхУчётГруппы.obj";

        [TestMethod]
        public void GenerateSBRepObject()
        {
            var mesh = StandardMeshReader.ReadMesh(Sample_GroundWithInfluencePath);
            var result = SBRepBuilder.BuildPlanarGroups(mesh);

            Assert.IsNotNull(result);
            Assert.AreEqual(7, result.Count);
            var loops = result.Select(x => x.GetLoops()).ToList();
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
    }


}
