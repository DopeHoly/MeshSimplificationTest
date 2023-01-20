using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MeshSimplificationTest
{
    //public class SBRepVertex 
    //{
    //    public g3.Vector3d Coordinate { get; set; }
    //}

    //public class SBRepEdge 
    //{
    //    public List<SBRepVertex> Vertices { get; set; }
    //}

    //public class SBRepLoop
    //{
    //    public List<SBRepEdge> Edges { get; set; }
    //}

    //public class SBRepFace
    //{
    //    public List<SBRepLoop> Loops { get; set; }
    //    public g3.Vector3d Normal { get; set; }
    //}

    //public class SBRepShell
    //{
    //    public List<SBRepFace> Faces { get; set; }
    //}

    public static class BrepTools
    {
        //public static void CreateBrep()
        //{
        //    Rhino.Geometry.Mesh mesh = new Rhino.Geometry.Mesh();
        //    mesh.Vertices.Add(0.0, 0.0, 1.0); //0
        //    mesh.Vertices.Add(1.0, 0.0, 1.0); //1
        //    mesh.Vertices.Add(2.0, 0.0, 1.0); //2
        //    mesh.Vertices.Add(3.0, 0.0, 0.0); //3
        //    mesh.Vertices.Add(0.0, 1.0, 1.0); //4
        //    mesh.Vertices.Add(1.0, 1.0, 2.0); //5
        //    mesh.Vertices.Add(2.0, 1.0, 1.0); //6
        //    mesh.Vertices.Add(3.0, 1.0, 0.0); //7
        //    mesh.Vertices.Add(0.0, 2.0, 1.0); //8
        //    mesh.Vertices.Add(1.0, 2.0, 1.0); //9
        //    mesh.Vertices.Add(2.0, 2.0, 1.0); //10
        //    mesh.Vertices.Add(3.0, 2.0, 1.0); //11

        //    mesh.Faces.AddFace(0, 1, 5, 4);
        //    mesh.Faces.AddFace(1, 2, 6, 5);
        //    mesh.Faces.AddFace(2, 3, 7, 6);
        //    mesh.Faces.AddFace(4, 5, 9, 8);
        //    mesh.Faces.AddFace(5, 6, 10, 9);
        //    mesh.Faces.AddFace(6, 7, 11, 10);
        //    mesh.Normals.ComputeNormals();
        //    mesh.Compact();
        //    var brep = Brep.CreateFromMesh(mesh, true);
        //}
    }

}
