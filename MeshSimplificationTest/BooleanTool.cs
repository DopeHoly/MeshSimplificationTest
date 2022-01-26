using g3;
using gs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest
{
    public class BooleanTool
    {
        private static Func<BoundedImplicitFunction3d, int, DMesh3> GenerateMeshF = (root, numcells) => {
            MarchingCubes c = new MarchingCubes();
            c.Implicit = root;
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;      // cube-edge convergence method
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

        public static DMesh3 Intersection(DMesh3 a, DMesh3 b)
        {
            var cellCount = 512;
            var offset = 0.1;
            try
            {
                // using half res SDF and full res remesh produces best results for me
                var sdfA = MeshToImplicitF(a, cellCount / 2, offset);
                var sdfB = MeshToImplicitF(b, cellCount / 2, offset);

                var diff = new ImplicitDifference3d { A = sdfA, B = sdfB };
                var m = GenerateMeshF(diff, cellCount);
                var remeshTool = new RemeshTool()
                {
                    EdgeLength = 1
                };
                var mesh = remeshTool.Remesh(m,
                    System.Threading.CancellationToken.None);
                return mesh;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            
            return null;
        }
    }
}
