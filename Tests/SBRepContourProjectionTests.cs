using g3;
using MeshSimplificationTest.SBRep;
using MeshSimplificationTest.SBRep.SBRepOperations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
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
            var path = new DirectoryInfo(SamplesPath);

            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(3, 5));
            contour.Add(new Vector2d(5, 3));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));

            var mesh = StandardMeshReader.ReadMesh(Sample_Cube4x4Path);
            var sbRep = SBRepBuilder.Convert(mesh);
            sbRep.ContourProjection(contour, true);

        }

        [TestMethod]
        public void PointInContour()
        {
            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(3, 5));
            contour.Add(new Vector2d(5, 3));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));

            Assert.IsTrue(Geometry2DHelper.InContour(contour, new Vector2d(1,1), 1e-6));
            Assert.IsTrue(Geometry2DHelper.InContour(contour, new Vector2d(2,2), 1e-6));
            Assert.IsTrue(Geometry2DHelper.InContour(contour, new Vector2d(3.5,2.0), 1e-6));
            Assert.IsTrue(Geometry2DHelper.InContour(contour, new Vector2d(3, 2), 1e-6));


            Assert.IsFalse(Geometry2DHelper.InContour(contour, new Vector2d(1, 5), 1e-6));
            Assert.IsFalse(Geometry2DHelper.InContour(contour, new Vector2d(4, 4), 1e-6));
            Assert.IsFalse(Geometry2DHelper.InContour(contour, new Vector2d(7, 9), 1e-6));
            Assert.IsFalse(Geometry2DHelper.InContour(contour, new Vector2d(3, -1), 1e-6));
            Assert.IsFalse(Geometry2DHelper.InContour(contour, new Vector2d(1.5, 0.5), 1e-6));

        }

        [TestMethod]
        public void IntersectContourGetPoints()
        {
            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(3, 5));
            contour.Add(new Vector2d(5, 3));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));

            var intersectContour = IntersectContour.FromPoints(contour);
            var counter = 0;
            Assert.AreEqual(contour.Count, intersectContour.GetContour().ToList().Count);
            foreach (var point in intersectContour.GetContour())
            {
                Assert.AreEqual(contour[counter], point.Coord);
                ++counter;
            }
        }

        [TestMethod]
        public void IntersectContourGetEdges()
        {
            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(3, 5));
            contour.Add(new Vector2d(5, 3));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));

            var intersectContour = IntersectContour.FromPoints(contour);
            Assert.AreEqual(contour.Count, intersectContour.GetEdges().ToList().Count);
            var indexes = intersectContour.GetEdges().Select(x => new Index2i(x.Item1.ID, x.Item2.ID)).ToList();
            var i = 0;
            foreach(var index in indexes)
            {
                var a = i;
                var b = i + 1;
                if (b == contour.Count)
                    b = 0;
                Assert.AreEqual(new Index2i(a, b), index);
                ++i;
            }
        }


        [TestMethod]
        public void CheckCrossEdgeVertex()
        {
            var contour = new List<Vector2d>();

            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 4));
            contour.Add(new Vector2d(4, 4));
            contour.Add(new Vector2d(4, 0));
            var intersectContour = IntersectContour.FromPoints(contour);
            var cross = IntersectContour.CalcEdgePositions(new Vector2d(3, 5), new Vector2d(5, 3), intersectContour, 1e-6);

            Assert.AreEqual(EdgePositionMode.Cross, cross.Mode);
            Assert.AreEqual(1, cross.Crosses);
            Assert.AreEqual(EdgeIntersectionType.ExistingPoint, cross.Crosses.First().IntersectionType);
            Assert.IsTrue(Geometry2DHelper.EqualPoints(new Vector2d(4, 4), cross.Crosses.First().Point0));
        }
    }
}
