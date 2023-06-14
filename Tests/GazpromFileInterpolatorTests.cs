using g3;
using GeometryLib;
using SBRep;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using SBRep.Utils;

namespace Tests
{
    [TestClass]
    public class GazpromFileInterpolatorTests
    {
        [TestMethod]
        public void ReadPlyFile()
        {
            var path = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var mesh = plyReader.Read(path);
            Assert.IsNotNull(mesh);
        }

        [TestMethod]
        public void DetectBoundaryEdges()
        {
            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var meshA = plyReader.Read(pathA);
            var meshB = plyReader.Read(pathB);
            var result = PlanesStapler.GetBindVertices(meshA, meshB);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count() > 0);
        }

        [TestMethod]
        public void StaplerPlanes()
        {
            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var meshA = plyReader.Read(pathA);
            var meshB = plyReader.Read(pathB);
            var result = PlanesStapler.StaplerTopBottomPlanes(meshA, meshB);
        }

        [TestMethod]
        public void StaplerPlanesTest()
        {
            //var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\testTop.ply";
            //var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\testBot.ply";

            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var meshA = plyReader.Read(pathA);
            var meshB = plyReader.Read(pathB);
            var result = PlanesStapler.TestStapler(meshA, meshB);

            var path = @"D:\Задачи\Газпром грунты\формат ply+свойства\output2.obj";
            WriteOptions writeOptions = new WriteOptions();
            writeOptions.bPerVertexColors = true;
            writeOptions.bWriteGroups = true;
            StandardMeshWriter.WriteFile(
           path,
           new List<WriteMesh>() { new WriteMesh(result) },
           writeOptions);
        }
        [TestMethod]
        public void StaplerTest()
        {
            //var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\testTop.ply";
            //var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\testBot.ply";

            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var meshA = plyReader.Read(pathA);
            var meshB = plyReader.Read(pathB);
            var result = PlanesStapler.Stapler(meshA, meshB);

            var path = @"D:\Задачи\Газпром грунты\формат ply+свойства\output3.obj";
            WriteOptions writeOptions = new WriteOptions();
            writeOptions.bPerVertexColors = true;
            writeOptions.bWriteGroups = true;
            StandardMeshWriter.WriteFile(
           path,
           new List<WriteMesh>() { new WriteMesh(result) },
           writeOptions);
        }

        [TestMethod]
        public void StaplerSBRepPlanesTest()
        {
            //var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\testTop.ply";
            //var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\testBot.ply";

            var pathA = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_top.ply";
            var pathB = @"D:\Задачи\Газпром грунты\формат ply+свойства\110000_bot.ply";
            var plyReader = new PlyReader();
            var meshA = plyReader.Read(pathA);
            var meshB = plyReader.Read(pathB);
            var result = PlanesStapler.SbrepTestStapler(meshA, meshB);

            var path = @"D:\Задачи\Газпром грунты\формат ply+свойства\output2.sbrep";
            SBRepIO.Write(result, path);
        }


        [TestMethod]
        public void Trianglulation()
        {
            var vertices = new List<g3.Vector2d>();
            var edges = new List<g3.Index2i>();

            vertices.Add(new g3.Vector2d(1, 2));
            vertices.Add(new g3.Vector2d(1, 3));
            vertices.Add(new g3.Vector2d(2, 3));
            vertices.Add(new g3.Vector2d(2, 2));

            vertices.Add(new g3.Vector2d(3, 1));
            vertices.Add(new g3.Vector2d(3, 4));

            edges.Add(new g3.Index2i(0, 1));
            edges.Add(new g3.Index2i(1, 2));
            edges.Add(new g3.Index2i(2, 3));
            edges.Add(new g3.Index2i(3, 0));

            edges.Add(new g3.Index2i(0, 4));
            edges.Add(new g3.Index2i(4, 5));
            edges.Add(new g3.Index2i(5, 1));

            IEnumerable<g3.Vector2d> triVertices = null;
            IEnumerable<g3.Index3i> triangles = null;

            var noError = TriangulationCaller.CDT(
                vertices, edges,
                out triVertices, out triangles);

            Assert.IsTrue(noError);
            Assert.AreEqual(8, triVertices.Count());
            Assert.AreEqual(8, triangles.Count());
        }

    }
}
