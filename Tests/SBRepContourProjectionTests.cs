using g3;
using MeshSimplificationTest.SBRep;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class SBRepContourProjectionTests
    {
        private static string SamplesPath = "../../../../samples/";
        private static string Sample_Cube4x4Path = SamplesPath + "Кубик4Fix.obj";
        private static string Sample_PipePath = SamplesPath + "pipe.obj";

        private SBRepObject GetSampleSBRep()
        {
            var obj = new SBRepObject();

            var vertices = new List<Vector3d>();
            var edges = new List<Index2i>();
            var verges = new List<ICollection<int>>();
            
            //topSides
            vertices.Add(new Vector3d(0, 0, 0));
            vertices.Add(new Vector3d(0, 4, 0));
            vertices.Add(new Vector3d(4, 4, 0));
            vertices.Add(new Vector3d(4, 0, 0));
            edges.Add(new Index2i(0, 1));
            edges.Add(new Index2i(1, 2));
            edges.Add(new Index2i(2, 3));
            edges.Add(new Index2i(3, 0));

            
            obj.RedefineFeedbacks();
            return obj;
        }

        [TestMethod]
        public void BooleanTest()
        {
            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));

            var mesh = StandardMeshReader.ReadMesh(Sample_PipePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            SBRepOperations.ContourProjection(sbRep, contour, true);

        }
    }
}
