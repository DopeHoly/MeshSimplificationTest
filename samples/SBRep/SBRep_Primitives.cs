using g3;
using SurfaceInterpolation.Tools.SBRep.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurfaceInterpolation.Tools.SBRep
{
    public abstract class SBRep_Primitive : IIndexed
    {
        public const int UndefinedIndex = -1;
        public int ID { get; set; } = -1;

        public SBRep_Primitive(int id = UndefinedIndex)
        {
            ID = id;
        }
    }

    public interface IIndexedVector3d : IIndexed
    {
        Vector3d Coordinate { get; }
    }

    public class SBRep_Vtx : SBRep_Primitive, IIndexedVector3d
    {
        public Vector3d Coordinate { get; set; }
        public ICollection<int> Parents;
        public SBRep_Vtx() : base()
        {
            Parents = new List<int>();
        }

        public SBRep_Vtx(SBRep_Vtx other) : base(other.ID)
        {
            Coordinate = other.Coordinate;
            Parents = new List<int>(other.Parents);
        }

        public override string ToString()
        {
            return $"Вершина {ID}: {Coordinate};";
        }
    }

    public class SBRep_Edge : SBRep_Primitive
    {
        public Index2i Vertices;
        public int Parent = -1;

        public SBRep_Edge() : base() { }
        public SBRep_Edge(SBRep_Edge other) : base(other.ID)
        {
            Vertices = other.Vertices;
            Parent = other.Parent;
        }

        public override string ToString()
        {
            return $"Ребро {ID}: {Vertices};";
        }
    }

    public class SBRep_Verge : SBRep_Primitive
    {
        public ICollection<int> Edges;
        public ICollection<int> Parents;

        public SBRep_Verge() : base()
        {
            Edges = new List<int>();
            Parents = new List<int>();
        }
        public SBRep_Verge(SBRep_Verge other) : base(other.ID)
        {
            Edges = new List<int>(other.Edges);
            Parents = new List<int>(other.Parents);
        }

        public override string ToString()
        {
            string edges = "(";
            foreach (int edge in Edges)
            {
                edges += $"{edge},";
            }
            edges += ")";
            return $"Ломанная {ID}: {edges};";
        }
    }

    public class SBRep_Loop : SBRep_Primitive
    {
        public ICollection<int> Verges;
        public ICollection<int> Parents;
        public SBRep_Loop() : base()
        {
            Verges = new List<int>();
            Parents = new List<int>();
        }
        public SBRep_Loop(SBRep_Loop other) : base(other.ID)
        {
            Verges = new List<int>(other.Verges);
            Parents = new List<int>(other.Parents);
        }

        public override string ToString()
        {
            string verges = "(";
            foreach (int edge in Verges)
            {
                verges += $"{edge},";
            }
            verges += ")";
            return $"Петля {ID}: {verges};";
        }
    }
    public class SBRep_Face : SBRep_Primitive
    {
        public int GroupID = -1;
        public Vector3d Normal;
        public PlaneFace Plane;
        public int OutsideLoop;
        public ICollection<int> InsideLoops;

        public SBRep_Face() : base()
        {
            InsideLoops = new List<int>();
        }

        public SBRep_Face(SBRep_Face other) : base(other.ID)
        {
            GroupID = other.GroupID;
            Normal = other.Normal;
            Plane = other.Plane;
            OutsideLoop = other.OutsideLoop;
            InsideLoops = new List<int>(other.InsideLoops);
        }

        public override string ToString()
        {
            string insideLoops = "(";
            foreach (int edge in InsideLoops)
            {
                insideLoops += $"{edge},";
            }
            insideLoops += ")";
            var builder = new StringBuilder();
            builder.AppendLine($"[Грань {ID};");
            builder.AppendLine($"[Normal: {Normal};");
            builder.AppendLine($"[Уравнение плоскости: {Plane};");
            builder.AppendLine($"[Внешняя петля: {OutsideLoop};");
            if (InsideLoops.Count > 0)
                builder.AppendLine($"[Внутренние петли: {insideLoops};");
            builder.AppendLine($"]");
            return builder.ToString();
        }
    }
}
