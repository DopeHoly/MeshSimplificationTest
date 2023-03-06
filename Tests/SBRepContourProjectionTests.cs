using g3;
using MeshSimplificationTest.SBRep;
using MeshSimplificationTest.SBRep.Utils;
using MeshSimplificationTest.SBRepVM;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Tests
{
    [TestClass]
    public class SBRepContourProjectionTests
    {
        private static string SamplesPath = "../../../../samples/";
        private static string Sample_Cube4x4Path = SamplesPath + "Кубик4Fix.obj";
        private static string Sample_PipePath = SamplesPath + "pipe.obj";
        private static string Sample_LargePipePath = SamplesPath + "Труба большая.obj";
        private static string Sample_CubeImplicitOnSidePath = SamplesPath + "CubeImplicitOnSide.obj";

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

        private List<Vector2d> SampleContour()
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

            return contour;
        }

        public List<Vector2d> OffsetContour(List<Vector2d> contour, double x, double y)
        {
            return contour.Select(point => new Vector2d(point.x + x, point.y + y)).ToList();
        }

        [TestMethod]
        public void BooleanTest()
        {
            var path = new DirectoryInfo(SamplesPath);

            var contour = SampleContour();

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
            var contour = SampleContour();

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
            var contour = SampleContour();

            var intersectContour = IntersectContour.FromPoints(contour);
            Assert.AreEqual(contour.Count, intersectContour.GetEdges().ToList().Count);
            var indexes = intersectContour.GetEdges().Select(x => x.Points).ToList();
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
            var cross = IntersectContour.CalcEdgePositions(new Vector2d(3, 5), new Vector2d(5, 3), -1, intersectContour, 1e-6);

            Assert.AreEqual(EdgePositionMode.Cross, cross.Mode);
            Assert.AreEqual(1, cross.Crosses.Count());
            Assert.AreEqual(2, cross.Crosses.First().VtxID);
            Assert.AreEqual(EdgeIntersectionType.ExistingPoint, cross.Crosses.First().IntersectionType);
            Assert.IsTrue(Geometry2DHelper.EqualPoints(new Vector2d(4, 4), cross.Crosses.First().Point0));
        }

        [TestMethod]
        public void IntersectTest()
        {
            var contour = SampleContour();

            var objectLoop = new List<Vector2d>();
            objectLoop.Add(new Vector2d(0, 0));
            objectLoop.Add(new Vector2d(0, 4));
            objectLoop.Add(new Vector2d(4, 4));
            objectLoop.Add(new Vector2d(4, 0));


            var intersectContour = IntersectContour.FromPoints(contour);
            var intersectLoop = IntersectContour.FromPoints(objectLoop);
            var result = IntersectContour.Intersect(intersectContour, intersectLoop);
            Assert.AreEqual(12, result.Count);
            var report = result.ToString();
            var ExpectContour = new List<Vector2d>();
            ExpectContour.Add(new Vector2d(0, 0));
            ExpectContour.Add(new Vector2d(0, 4));
            ExpectContour.Add(new Vector2d(0, 5));
            ExpectContour.Add(new Vector2d(3, 5));
            ExpectContour.Add(new Vector2d(4, 4));
            ExpectContour.Add(new Vector2d(5, 3));
            ExpectContour.Add(new Vector2d(4, 2));
            ExpectContour.Add(new Vector2d(4, 0));
            ExpectContour.Add(new Vector2d(3, -1));
            ExpectContour.Add(new Vector2d(2.5, 0));
            ExpectContour.Add(new Vector2d(2, 1));
            ExpectContour.Add(new Vector2d(1, 1));
            int i = 0;
            foreach (var point in result.GetContour())
            {
                Assert.IsTrue(Geometry2DHelper.EqualPoints(ExpectContour[i], point.Coord));
                ++i;
            }
        }

        [TestMethod]
        public void SortPointOnEdgeTest()
        {
            var a = new Vector2d(0, 0);
            var b = new Vector2d(11, 0);
            var points = new Dictionary<int, Vector2d>();
            points.Add(0, new Vector2d(0, 0));
            points.Add(1, new Vector2d(2, 0));
            points.Add(4, new Vector2d(7, 0));
            points.Add(7, new Vector2d(11, 0));
            points.Add(6, new Vector2d(9, 0));
            points.Add(5, new Vector2d(8, 0));
            points.Add(2, new Vector2d(3, 0));
            points.Add(3, new Vector2d(5, 0));
            var result = Geometry2DHelper.SortPointsOnEdge(a, b, points);
            Assert.AreEqual(points.Count, result.Count);
            for(int i = 0; i <= 7; ++i)
            {
                Assert.IsTrue(result.ContainsKey(i));
                Assert.AreEqual(points[i], result[i]);
            }
        }

        [TestMethod]
        public void LargePipe()
        {
            var contour = OffsetContour(SampleContour(), 15, 15);
            var mesh = StandardMeshReader.ReadMesh(Sample_LargePipePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            sbRep.ContourProjection(contour, true);
        }

        [TestMethod]
        public void InsertPointToEdge()
        {
            var contour = SampleContour();
            var mesh = StandardMeshReader.ReadMesh(Sample_Cube4x4Path);
            var sbRep = SBRepBuilder.Convert(mesh);
            //sbRep.ContourProjection(contour, true);
            var loopVertices = sbRep.GetClosedContourVtx(3);
            var edgesLoop = sbRep.GetClosedContourEdgesFromLoopID(3);
            var points = new List<SBRep_Vtx>();
            points.Add(new SBRep_Vtx()
            {
                ID = 200,
                Coordinate = new Vector3d(1, 0, 4),
            });
            points.Add(new SBRep_Vtx()
            {
                ID = 201,
                Coordinate = new Vector3d(2, 0, 4),
            });
            points.Add(new SBRep_Vtx()
            {
                ID = 202,
                Coordinate = new Vector3d(3, 0, 4),
            });
            var addedEdges = sbRep.AddPointsOnEdge(7, points);
            var edges = sbRep.GetClosedContourEdgesFromLoopID(3);
        }

        [TestMethod]
        public void ImplicidCube()
        {
            var contour = new List<Vector2d>();
            contour.Add(new Vector2d(0, 0));
            contour.Add(new Vector2d(0, 5));
            contour.Add(new Vector2d(4, 2));
            contour.Add(new Vector2d(4, 0));
            contour.Add(new Vector2d(3, -1));
            contour.Add(new Vector2d(2, 1));
            contour.Add(new Vector2d(1, 1));
            contour = OffsetContour(contour, 26, 30);

            var mesh = StandardMeshReader.ReadMesh(Sample_CubeImplicitOnSidePath);
            var sbRep = SBRepBuilder.Convert(mesh);
            sbRep.ContourProjection(contour, true);
        }
        private static List<Vector2d> LoadContour(string path)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Vector2d>));
            List<Vector2d> result = null;
            using (var reader = new StreamReader(path))
            {
                result = xmlSerializer.Deserialize(reader) as List<Vector2d>;
            }
            return result;
        }

        private IEnumerable<List<Vector2d>> GetContours(string dir)
        {
            var contours = new List<List<Vector2d>>();

            var dirInfo = new DirectoryInfo(dir);
            var contoursFiles = dirInfo.GetFiles("*.cnt");
            foreach (var file in contoursFiles)
            {
                var contour = LoadContour(file.FullName);
                if (contour == null) continue;
                contours.Add(contour);
            }
            return contours;
        }

        private DMesh3 GetMeshInDir(string dir)
        {
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.GetFiles("*.obj");
            return StandardMeshReader.ReadMesh(files.First().FullName);
        }

        [TestMethod]
        public void ContourProjectionDDT()
        {
            var samplePath = SamplesPath + "ForUnitTests";
            var sampleDir = new DirectoryInfo(samplePath);
            var samplesPaths = sampleDir.GetDirectories().Select(x => x.FullName);
            var failedTests = new List<string>();
            foreach (var currentTestPath in samplesPaths)
            {                
                try
                {
                    var mesh = GetMeshInDir(currentTestPath);
                    var sbRep = SBRepBuilder.Convert(mesh);
                    foreach (var contour in GetContours(currentTestPath))
                    {
                        sbRep = sbRep.ContourProjection(contour, true);
                    }
                }
                catch
                {
                    failedTests.Add(currentTestPath);
                }
            }
           
            if(failedTests.Count > 0)
            {
                Console.WriteLine("Завалившиеся тесты:");
                foreach (var item in failedTests)
                {
                    Console.WriteLine($" - {item}");
                }
            }
            Assert.IsTrue(failedTests.Count == 0);
        }

        //[TestMethod]
        //public void SortByPolarTest()
        //{
        //    var beginPoint = Vector2d.Zero;
        //    var edgeLastPoint = new Vector2d(1,1);

        //    var pointsForSort = new List<Vector2d>()
        //    {
        //        new Vector2d(1, 0),
        //        new Vector2d(1, -1),
        //        new Vector2d(-1, -1),
        //        new Vector2d(-1, 1),
        //        new Vector2d(0, 1),
        //    };

        //    var result = SBRepOperationsExtension.SortByPolarT(
        //        pointsForSort,
        //        beginPoint,
        //        edgeLastPoint);
        //}

        [TestMethod]
        public void SignedAreaTest()
        {
            var points = new List<Vector2d>()
            {
                new Vector2d(0, 0),
                new Vector2d(0, 1),
                new Vector2d(1, 1),
                new Vector2d(1, 0),
            };
            var areaCW = Geometry2DHelper.GetAreaSigned(points);
            points.Reverse();
            var areaCCW = Geometry2DHelper.GetAreaSigned(points);
            Assert.AreEqual(Math.Abs(areaCW), Math.Abs(areaCCW));
            Assert.AreNotEqual(Math.Sign(areaCW), Math.Sign(areaCCW));
        }

    }
}
