using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MeshSimplificationTest.SBRep.Triangulation
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2D
    {
        public double X;
        public double Y;

        public Vector2D(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Edge2i
    {
        public int a;
        public int b;

        public Edge2i(int A, int B)
        {
            a = A;
            b = B;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TriangleVIndexes
    {
        public int a;
        public int b;
        public int c;

        public TriangleVIndexes(int A, int B, int C)
        {
            a = A;
            b = B;
            c = C;
        }
    }

    public static class TriangulationCaller
    {
        [DllImport("..\\..\\..\\..\\..\\MeshSimplificationTest\\CDT_Simmakers\\bin\\x64\\Debug\\CDT_Simmakers.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Triangulate(
            int verticesCount, 
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] Vector2D[] vertices,
            int edgesCount,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 1)] Edge2i[] edges,
            out int triangleVerticesCount,
            out IntPtr triangleVertices,
            out int trianglesCount,
            out IntPtr triangles);

        public static bool CDT(
            IEnumerable<g3.Vector2d> vertices,
            IEnumerable<g3.Index2i> edges,
            out IEnumerable<g3.Vector2d> triVertices,
            out IEnumerable<g3.Index3i> triangles)
        {
            var verticeArray = vertices.Select(vtx => new Vector2D(vtx.x, vtx.y)).ToArray();
            var edgesArray = edges.Select(edge => new Edge2i(edge.a, edge.b)).ToArray();

            int triangleVerticesCount = -1;
            int trianglesCount = -1;
            IntPtr verticesPtr;
            IntPtr trianglesPtr;

            var noError = Triangulate(
                verticeArray.Length, verticeArray,
                edgesArray.Length,  edgesArray,
                out triangleVerticesCount, out verticesPtr,
                out trianglesCount,out trianglesPtr);

            if (triangleVerticesCount == -1 || trianglesCount == -1)
            {
                triVertices = null;
                triangles = null;
                return false;
            }

            var meshVertices = CopyStructArrayFromPointer<Vector2D>(verticesPtr, triangleVerticesCount);
            var meshTriangles = CopyStructArrayFromPointer<TriangleVIndexes>(trianglesPtr, trianglesCount);

            triVertices = meshVertices.Select(vtx => new g3.Vector2d(vtx.X, vtx.Y)).ToList();
            triangles = meshTriangles.Select(triangle => new g3.Index3i(triangle.a, triangle.b, triangle.c)).ToList();

            return noError;
        }

        private static IEnumerable<T> CopyStructArrayFromPointer<T>(IntPtr pointer, int size)
            where T: struct
        {
            T[] resultArray = new T[size];
            IntPtr current = pointer;
            for (int i = 0; i < size && current != IntPtr.Zero; i++)
            {
                //resultArray[i] = new T();
                resultArray[i] = (T)Marshal.PtrToStructure(current, typeof(T));
                Marshal.DestroyStructure(current, typeof(T));
                current = (IntPtr)((long)current + Marshal.SizeOf(resultArray[i]));
            }

            //Marshal.FreeCoTaskMem(pointer);

            return resultArray;
        }
    }
}
