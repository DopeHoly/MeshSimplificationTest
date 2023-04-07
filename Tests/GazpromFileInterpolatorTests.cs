using GeometryLib;
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
            var result = PlanesStapler.GetBindVertex(meshA, meshB);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count() > 0);
        }
    }
}
