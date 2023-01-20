using g3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshSimplificationTest.SBRep
{

    public class SBRepObject
    {
        public IndexedCollection<SBRep_Vtx> _vertices { get; private set; }
        public IndexedCollection<SBRep_Edge> _edges { get; private set; }
        public IndexedCollection<SBRep_LoopEdge> _loopPart { get; private set; }
        public IndexedCollection<SBRep_Loop> _loops { get; private set; }
        public IndexedCollection<SBRep_Face> _faces { get; private set; }

        public SBRepObject()
        {
            _vertices = new IndexedCollection<SBRep_Vtx>();
            _edges = new IndexedCollection<SBRep_Edge>();
            _loopPart = new IndexedCollection<SBRep_LoopEdge>();
            _loops = new IndexedCollection<SBRep_Loop>();
            _faces = new IndexedCollection<SBRep_Face>();
        }

        #region Data Access
        public IEnumerable<SBRep_Face> GetFaces()
        {
            return _faces;
        }
        public IEnumerable<int> GetFacesIds()
        {
            return _faces.GetIndexes();
        }

        public SBRep_Face GetFace(int id)
        {
            return _faces[id];
        }

        public Vector3d GetVertex(int id)
        {
            return _vertices[id].Coordinate;
        }

        public IEnumerable<Vector3d> GetVertices()
        {
            return _vertices.Select(x => x.Coordinate);
        }
        public IEnumerable<int> GetVerticesIds()
        {
            return _vertices.GetIndexes();
        }
        #endregion

    }
}
