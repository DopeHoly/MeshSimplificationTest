using g3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class PlanesStapler
    {
        public static IEnumerable<int> GetBoundaryEdges(DMesh3 mesh)
        {
            return mesh.EdgeIndices().Where(x => {
                var edgeT = mesh.GetEdgeT(x);
                return edgeT.a == -1 || edgeT.b == -1;
                }).ToList();
        }

        public static IEnumerable<int> GetUniqueVerticesIDFromEdgeIDs(DMesh3 mesh, IEnumerable<int> edges)
        {
            return mesh.EdgeIndices().SelectMany(x =>
            {
                var edgeV = mesh.GetEdgeV(x);
                return new[]{ edgeV.a, edgeV.b};
            }).Distinct().ToList();
        }

        public static IEnumerable<int> GetBoundaryVertices(DMesh3 mesh)
        {
            return GetUniqueVerticesIDFromEdgeIDs(mesh, GetBoundaryEdges(mesh));
        }

        public static Dictionary<int, Vector3d> GetVerticesCoordinate(DMesh3 mesh, IEnumerable<int> vids)
        {
            var dict = new Dictionary<int, Vector3d>();
            foreach (var v in vids)
            {
                dict.Add(v, mesh.GetVertex(v));
            }
            return dict;
        }

        public static IEnumerable<Index2i> GetBindVertex(DMesh3 meshA, DMesh3 meshB)
        {
            var boundaryVerticesA = GetBoundaryVertices(meshA);
            var boundaryVerticesB = GetBoundaryVertices(meshB);

            var boundaryVCoordA = GetVerticesCoordinate(meshA, boundaryVerticesA);
            var boundaryVCoordB = GetVerticesCoordinate(meshB, boundaryVerticesB);

            var vtxACount = boundaryVerticesA.Count();
            var vtxBCount = boundaryVerticesB.Count();

            var distanceMtx = new double[vtxACount, vtxBCount];

            int aIndex = 0; int bIndex = 0;

            foreach (var vtxA in boundaryVCoordA)
            {
                bIndex = 0;
                foreach (var vtxB in boundaryVCoordB)
                {
                    distanceMtx[aIndex, bIndex] = (vtxA.Value.xy - vtxB.Value.xy).LengthSquared;
                    ++bIndex;
                }
                ++aIndex;
            }

            var connection = new List<Index2i>();

            aIndex = 0;
            foreach (var vtxA in boundaryVCoordA)
            {
                var minValue = double.MaxValue;
                var minIndex = -1;
                for(int i = 0; i < vtxBCount; ++i)
                {
                    var value = distanceMtx[aIndex, i];
                    if (value < minValue)
                    {
                        minIndex = i;
                        minValue = value;
                    }
                }
                Debug.Assert(minIndex != -1);

                connection.Add(new Index2i(aIndex, boundaryVCoordB.ElementAt(minIndex).Key));

                ++aIndex;
            }

            bIndex = 0;
            foreach (var vtxA in boundaryVCoordB)
            {
                var minValue = double.MaxValue;
                var minIndex = -1;
                for (int i = 0; i < vtxACount; ++i)
                {
                    var value = distanceMtx[i, bIndex];
                    if (value < minValue)
                    {
                        minIndex = i;
                        minValue = value;
                    }
                }
                Debug.Assert(minIndex != -1);

                connection.Add(new Index2i(boundaryVCoordA.ElementAt(minIndex).Key, bIndex));

                ++bIndex;
            }
            connection = connection.Distinct().ToList();

            return connection;
        }
    }
}
