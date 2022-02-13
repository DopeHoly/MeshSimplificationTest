using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest
{
    public class GeometryUtils
    {
        public static void DeleteDegenerateTriangle(DMesh3 mesh, double tresholdAngle)
        {
            var degTriList = new Dictionary<int, int>();
            foreach (int triangleid in mesh.TriangleIndices())
            {
                if (IsDegenerativeTriangle(mesh, triangleid))
                {
                    var triangle = mesh.GetTriangle(triangleid);
                    var pointId = GetDegenerateVtxId(mesh, triangleid);
                    degTriList.Add(triangleid, pointId);
                }
            }
            var orderTriList = new Dictionary<int, List<int>>();
            var listDegeneratePoint = degTriList.Values.ToList();
            var postProcList = new Queue<KeyValuePair<int, List<int>>>();
            foreach (var item in degTriList)
            {
                var neighbors = GetNeighborVtx(mesh, item.Value);
                var equals = EqualsElements(
                        listDegeneratePoint,
                        neighbors);
                if (neighbors.Count == equals.Count)
                {
                    //cntEquals = int.MaxValue;
                    postProcList.Enqueue(new KeyValuePair<int, List<int>>(item.Key, equals));
                }
                orderTriList.Add(item.Key, equals);
            }
            var sortedDict = from entry in orderTriList orderby entry.Value.Count ascending select entry;
            foreach (var item in sortedDict)
            {
                if (TryFixTriangle(mesh, item.Key, tresholdAngle, degTriList, item.Value) == FixTriangleResult.Bad)
                {
                    postProcList.Enqueue(new KeyValuePair<int, List<int>>(item.Key, item.Value));
                }
            }
            while (postProcList.Count != 0)
            {
                var item = postProcList.Dequeue();
                if (TryFixTriangle(mesh, item.Key, tresholdAngle, degTriList, item.Value) == FixTriangleResult.Bad)
                {
                    postProcList.Enqueue(new KeyValuePair<int, List<int>>(item.Key, item.Value));
                }
            }
        }
        private enum FixTriangleResult
        {
            Good,
            Bad,
            Slip
        }
        private static FixTriangleResult TryFixTriangle(
            DMesh3 mesh,
            int tid,
            double tresholdAngle,
            Dictionary<int, int> degTriList,
            List<int> zeroNeighbors)
        {
            if (IsDegenerativeTriangle(mesh, tid))
            {
                var triangle = mesh.GetTriangle(tid);
                var pointId = degTriList[tid];
                var oldCoord = mesh.GetVertex(pointId);
                var center = MeshWeights.OneRingCentroid(mesh, pointId);
                //проверка, что лежит на одной плоскости
                if (OneRingVtxIsPlane(mesh, pointId, tresholdAngle))
                {
                    mesh.SetVertex(pointId, center);
                    foreach (var point2 in zeroNeighbors)
                    {
                        if (EdgesIntersect(mesh, pointId, point2))
                        {
                            mesh.SetVertex(pointId, oldCoord);
                            return FixTriangleResult.Slip;
                        }
                    }
                    int group = GetVtxRingFaceGroup(mesh, pointId);
                    return FixTriangleResult.Good;
                    //mesh.SetTriangleGroup(triangleid, group);
                }
                else
                    return FixTriangleResult.Slip;
            }
            return FixTriangleResult.Good;
        }

        /// <summary>
        /// Находит номер группы для нулевого треугольника вокруг вертекса vid
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        public static int GetVtxRingFaceGroup(DMesh3 mesh, int vid)
        {
            var triangles = new List<int>();
            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                var triangleGroups = triangles.Select(x =>
                {
                    if (IsDegenerativeTriangle(mesh, x))
                    {
                        return -1;
                    }
                    return mesh.GetTriangleGroup(x);
                }).ToList();
                var groups = new Dictionary<int, int>();
                foreach (var groupCurent in triangleGroups)
                {
                    if (!groups.ContainsKey(groupCurent))
                    {
                        groups.Add(groupCurent, 1);
                        continue;
                    }
                    ++groups[groupCurent];
                }
                var groupID = groups.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
                foreach (var tid in triangles)
                {
                    mesh.SetTriangleGroup(tid, groupID);
                }
                return groupID;
            }
            return -1;
        }


        public static double GetAngle(Vector3d vtx, Vector3d a, Vector3d b)
        {
            return Vector3DAngle(a - vtx, b - vtx);
        }

        public static bool IsDegenerativeTriangle(DMesh3 mesh, int tid, double treshhold = 179)
        {
            //treshhold = 1e-6;
            //var area = mesh.GetTriArea(tid);
            //if (area < treshhold)
            //{
            //    return true;
            //}
            //return false;
            var triangle = mesh.GetTriangle(tid);
            var a = mesh.GetVertex(triangle.a);
            var b = mesh.GetVertex(triangle.b);
            var c = mesh.GetVertex(triangle.c);
            var angleA = GetAngle(a, b, c);
            var angleB = GetAngle(b, a, c);
            var angleC = GetAngle(c, a, b);

            return angleA > treshhold || angleB > treshhold || angleC > treshhold;
        }

        public static int GetDegenerateVtxId(DMesh3 mesh, int tid)
        {
            var triangle = mesh.GetTriangle(tid);
            var a = mesh.GetVertex(triangle.a);
            var b = mesh.GetVertex(triangle.b);
            var c = mesh.GetVertex(triangle.c);
            var angleA = Vector3d.AngleD(b - a, c - a);
            var angleB = Vector3d.AngleD(a - b, c - b);
            var angleC = Vector3d.AngleD(a - c, b - c);
            var maxAngle = new[] { angleA, angleB, angleC }.Max();
            if (angleA == maxAngle)
            {
                return triangle.a;
            }
            if (angleB == maxAngle)
            {
                return triangle.b;
            }
            if (angleC == maxAngle)
            {
                return triangle.c;
            }
            return -1;
        }


        public static List<int> GetNeighborVtx(DMesh3 mesh, int vid)
        {
            var result = new List<int>();
            foreach (var eid in mesh.VtxEdgesItr(vid))
            {
                var edge = mesh.GetEdgeV(eid);
                var vtxAId = edge.a == vid ? edge.b : edge.a;
                result.Add(vtxAId);
            }
            return result;
        }
        public static List<int> GetNeighborEdge(DMesh3 mesh, int vid)
        {
            var result = new List<int>();
            foreach (var eid in mesh.VtxEdgesItr(vid))
            {
                result.Add(eid);
            }
            return result;
        }

        public static int CountEqualsElements(List<int> a, List<int> b)
        {
            int counter = 0;
            foreach (var item in a)
            {
                var elements = b.FindAll(x => x.Equals(item));
                if (elements.Count > 0)
                    ++counter;
            }
            return counter;
        }
        public static List<int> EqualsElements(List<int> a, List<int> b)
        {
            var result = new List<int>();
            foreach (var item in a)
            {
                var elements = b.FindAll(x => x.Equals(item));
                if (elements.Count > 0)
                    result.AddRange(elements);
            }
            return result;
        }

        public static bool EdgesIntersect(DMesh3 mesh, int vid1, int vid2)
        {
            var skipEdge = -1;
            foreach (var eid in mesh.VtxEdgesItr(vid1))
            {
                var edge = mesh.GetEdgeV(eid);
                var vtxAId = edge.a == vid1 ? edge.b : edge.a;
                if (vtxAId == vid2)
                {
                    skipEdge = eid;
                    break;
                }
            }
            var v1Edges = GetNeighborEdge(mesh, vid1);
            var v2Edges = GetNeighborEdge(mesh, vid2);
            foreach (var v1edge in v1Edges)
            {
                if (v1edge == skipEdge) continue;
                var edge = mesh.GetEdgeV(v1edge);
                var a = mesh.GetVertex(edge.a);
                var b = mesh.GetVertex(edge.b);
                foreach (var v2edge in v2Edges)
                {
                    if (v2edge == skipEdge) continue;
                    var edge2 = mesh.GetEdgeV(v2edge);
                    var c = mesh.GetVertex(edge2.a);
                    var d = mesh.GetVertex(edge2.b);
                    if (CalculateLineLineIntersection(a, b, c, d))
                        return true;
                }
            }
            return false;
        }

        private static Index3i ReplaceZeroTriangle(DMesh3 mesh, int vid, int badTriId)
        {
            var triangles = new List<int>(3);
            var trianglePoints = new List<int>(9);
            var templateTriangle = Index3i.Zero;
            var gid = -1;

            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                foreach (var tid in triangles)
                {
                    var triangle = mesh.GetTriangle(tid);
                    if (triangle.a != vid)
                        trianglePoints.Add(triangle.a);
                    if (triangle.b != vid)
                        trianglePoints.Add(triangle.b);
                    if (triangle.c != vid)
                        trianglePoints.Add(triangle.c);
                    if (tid != badTriId)
                    {
                        templateTriangle = triangle;
                        gid = mesh.GetTriangleGroup(tid);
                    }
                }
                var points = new List<int>(trianglePoints.Distinct());
                if (points.Count != 3)
                {
                    throw new Exception("Expeption create replace triangle.");
                }
                foreach (var tid in triangles)
                {
                    mesh.RemoveTriangle(tid);
                }

                if (vid.Equals(templateTriangle.a))
                {
                    points.Remove(templateTriangle.b);
                    points.Remove(templateTriangle.c);
                    templateTriangle.a = points.Last();
                }
                else
                if (vid.Equals(templateTriangle.b))
                {
                    points.Remove(templateTriangle.a);
                    points.Remove(templateTriangle.c);
                    templateTriangle.b = points.Last();
                }
                else
                if (vid.Equals(templateTriangle.c))
                {
                    points.Remove(templateTriangle.b);
                    points.Remove(templateTriangle.a);
                    templateTriangle.c = points.Last();
                }
                mesh.AppendTriangle(templateTriangle, gid);
                return templateTriangle;

            }
            return Index3i.Zero;
        }

        public static bool CalculateLineLineIntersection(Vector3d line1Point1, Vector3d line1Point2,
            Vector3d line2Point1, Vector3d line2Point2)
        {
            var point = Vector3d.Zero;
            var p1 = Vector3d.Zero;
            var p2 = Vector3d.Zero;
            if (!CalculateLineLineIntersection(
                    line1Point1, line1Point2,
                    line2Point1, line2Point2,
                    out p1, out p2
                ))
                return false;
            if (p1.Distance(p2) < 1e-5)
            {
                point = p1;
                //var str =
                //    $"{line1Point1.x}\t{line1Point1.y}\n" +
                //    $"{line1Point2.x}\t{line1Point2.y}\n" +
                //    $"{line2Point1.x}\t{line2Point1.y}\n" +
                //    $"{line2Point2.x}\t{line2Point2.y}\n" +
                //    $"{point.x}\t{point.y}\n";
                return
                    point.Distance(line1Point1) > 1e-5 &&
                    point.Distance(line1Point2) > 1e-5 &&
                    point.Distance(line2Point1) > 1e-5 &&
                    point.Distance(line2Point2) > 1e-5 &&
                    (PointOnLine3D(line1Point1, line1Point2, point) && PointOnLine3D(line2Point1, line2Point2, point));
            }
            else return false;
            //return PointOnLine3D(line1Point1, line1Point2, point) || PointOnLine3D(line2Point1, line2Point2, point);
        }

        /// <summary>
        /// Вычисляет сегмент линии пересечения между двумя линиями (не сегментами).
        /// Возвращает false, если решение не найдено.
        /// </summary>
        /// <returns></returns>
        public static bool CalculateLineLineIntersection(Vector3d line1Point1, Vector3d line1Point2,
            Vector3d line2Point1, Vector3d line2Point2, out Vector3d resultSegmentPoint1, out Vector3d resultSegmentPoint2)
        {
            var eps = 1e-5;
            // Algorithm is ported from the C algorithm of 
            // Paul Bourke at http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline3d/
            resultSegmentPoint1 = Vector3d.Zero;
            resultSegmentPoint2 = Vector3d.Zero;

            Vector3d p1 = line1Point1;
            Vector3d p2 = line1Point2;
            Vector3d p3 = line2Point1;
            Vector3d p4 = line2Point2;
            Vector3d p13 = p1 - p3;
            Vector3d p43 = p4 - p3;

            if (p43.LengthSquared < eps)
            {
                return false;
            }
            Vector3d p21 = p2 - p1;
            if (p21.LengthSquared < eps)
            {
                return false;
            }

            double d1343 = p13.x * (double)p43.x + (double)p13.y * p43.y + (double)p13.z * p43.z;
            double d4321 = p43.x * (double)p21.x + (double)p43.y * p21.y + (double)p43.z * p21.z;
            double d1321 = p13.x * (double)p21.x + (double)p13.y * p21.y + (double)p13.z * p21.z;
            double d4343 = p43.x * (double)p43.x + (double)p43.y * p43.y + (double)p43.z * p43.z;
            double d2121 = p21.x * (double)p21.x + (double)p21.y * p21.y + (double)p21.z * p21.z;

            double denom = d2121 * d4343 - d4321 * d4321;
            if (Math.Abs(denom) < eps)
            {
                return false;
            }
            double numer = d1343 * d4321 - d1321 * d4343;

            double mua = numer / denom;
            double mub = (d1343 + d4321 * (mua)) / d4343;

            resultSegmentPoint1.x = (float)(p1.x + mua * p21.x);
            resultSegmentPoint1.y = (float)(p1.y + mua * p21.y);
            resultSegmentPoint1.z = (float)(p1.z + mua * p21.z);
            resultSegmentPoint2.x = (float)(p3.x + mub * p43.x);
            resultSegmentPoint2.y = (float)(p3.y + mub * p43.y);
            resultSegmentPoint2.z = (float)(p3.z + mub * p43.z);

            return true;
        }

        public static bool PointOnLine3D(Vector3d a, Vector3d b, Vector3d p)
        {
            var eps = 1e-5;
            var x = p.x; var y = p.y; var z = p.z;
            var x1 = a.x; var y1 = a.y; var z1 = a.z;
            var x2 = b.x; var y2 = b.y; var z2 = b.z;
            var AB = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1) + (z2 - z1) * (z2 - z1));
            var AP = Math.Sqrt((x - x1) * (x - x1) + (y - y1) * (y - y1) + (z - z1) * (z - z1));
            var PB = Math.Sqrt((x2 - x) * (x2 - x) + (y2 - y) * (y2 - y) + (z2 - z) * (z2 - z));
            return Math.Abs(AP + PB - AB) < eps;
        }

        public static double Vector3DAngle(Vector3d v1, Vector3d v2)
        {
            double fDot = v1.Dot(v2);
            var mamb = v1.Length * v2.Length;
            return Math.Acos(fDot / mamb) * MathUtil.Rad2Deg;
        }

        public static double OneRingVtxMaxAngle(DMesh3 mesh, int vid)
        {
            var triangles = new List<int>();
            var angle = double.MinValue;
            if (mesh.GetVtxTriangles(vid, triangles, false) == MeshResult.Ok)
            {
                var normals = triangles.Select(x =>
                {
                    if (IsDegenerativeTriangle(mesh, x))
                    {
                        return Vector3d.Zero;
                    }
                    return mesh.GetTriNormal(x);
                }
                ).ToList();
                for (int i = 0; i < normals.Count; ++i)
                {
                    for (int j = 0; j < normals.Count; ++j)
                    {
                        if (i == j) continue;
                        var n1 = normals[i];
                        var n2 = normals[j];
                        if (!(n1.Length > 0.999 && n1.Length < 1.001) ||
                            !(n2.Length > 0.999 && n2.Length < 1.001)) continue;
                        angle = Math.Max(n1.AngleD(n2), angle);
                    }
                }
            }
            return angle;
        }

        public static bool OneRingVtxIsPlane(DMesh3 mesh, int vid, double tresholdAngle)
        {
            double angle = OneRingVtxMaxAngle(mesh, vid);
            return Math.Abs(angle) < tresholdAngle && angle != double.MinValue;
        }
        public static bool OneRingVtxIsSharp(DMesh3 mesh, int vid, double tresholdAngle)
        {
            double angle = OneRingVtxMaxAngle(mesh, vid);
            return Math.Abs(angle) > tresholdAngle && angle != double.MinValue;
        }

        public static bool IsSharpEdges(DMesh3 mesh, int vid, Dictionary<int, List<int>> stats, double thresholdAngle)
        {
            if (!stats.ContainsKey(vid))
                throw new ArgumentException();

            var edgeIds = stats[vid];
            if (edgeIds.Count != 2)
                return true;
            var vertex = mesh.GetVertex(vid);
            var minAngle = double.MaxValue;
            foreach (var edgeA in edgeIds)
            {
                var a = mesh.GetEdgeV(edgeA);
                var vtxAId = a.a == vid ? a.b : a.a;
                var vtxA = mesh.GetVertex(vtxAId);
                foreach (var edgeB in edgeIds)
                {
                    if (edgeA == edgeB) continue;

                    var b = mesh.GetEdgeV(edgeB);
                    var vtxBId = b.a == vid ? b.b : b.a;
                    var vtxB = mesh.GetVertex(vtxBId);
                    var avtx = vtxA - vertex;
                    var bvtx = vtxB - vertex;
                    var tempAngle = Vector3DAngle(avtx, bvtx);
                    minAngle = Math.Min(tempAngle, minAngle);
                }
            }
            return thresholdAngle >= minAngle;
        }

        public static double GetTargetEdgeLength(DMesh3 mesh)
        {
            double mine, maxe, avge;
            MeshQueries.EdgeLengthStats(mesh, out mine, out maxe, out avge);
            var coefList = new List<double>();
            var edgeLenList = new List<double>();
            foreach (var tid in mesh.TriangleIndices())
            {
                if (IsDegenerativeTriangle(mesh, tid))
                    continue;
                var triangle = mesh.GetTriangle(tid);
                var a = mesh.GetVertex(triangle.a);
                var b = mesh.GetVertex(triangle.b);
                var c = mesh.GetVertex(triangle.c);
                var angleA = GetAngle(a, b, c);
                var angleB = GetAngle(b, a, c);
                var angleC = GetAngle(c, a, b);
                var angles = new[] { angleA, angleB, angleC };
                var max = angles.Max();
                var min = angles.Min();
                var delta_a = Math.Abs(angleA - 60.0);
                var delta_b = Math.Abs(angleB - 60.0);
                var delta_c = Math.Abs(angleC - 60.0);
                //var coef = (180.0 - (ab + cb + ac)) / 180.0;
                var coef = 180.0 / (180.0 - ((delta_a + delta_b + delta_c)/3.0));
                var length = 0.0;
                if(angleA == min)
                {
                    length = b.Distance(c);
                }
                else
                if (angleB == min)
                {
                    length = a.Distance(c);
                }
                else
                if (angleC == min)
                {
                    length = b.Distance(a);
                }
                //var coef = min / max;
                coefList.Add(coef);
                edgeLenList.Add(length);
            }
            var resultCoef = coefList.Average();
            var resultLen = edgeLenList.Average();
            return (avge * 1.0 / 4.0 + resultLen * 3.0 / 4.0) / 2.0;
        }
    }

}
