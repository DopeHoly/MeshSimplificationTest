using g3;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MeshSimplificationTest.SBRep.SBRepBuilder;

namespace MeshSimplificationTest.SBRep
{
    public interface IIndexed
    {
        int ID { get; set; }
    }

    public abstract class SBRep_Primitive : IIndexed
    {
        public const int UndefinedIndex = -1;
        public int ID { get; set; } = -1;

    }

    public class SBRep_Vtx : SBRep_Primitive
    {
        public Vector3d Coordinate;
        public ICollection<int> Parents;
    }

    public class SBRep_Edge : SBRep_Primitive
    {
        public Index2i Vertices;
        public int Parent = -1;

        public void Separate(IEnumerable<int> vtxIds)
        {
            //TODO
        }
    }
    public class SBRep_Verge : SBRep_Primitive
    {
        public ICollection<int> Edges;
        public ICollection<int> Parents;
    }

    public class SBRep_Loop : SBRep_Primitive
    {
        public ICollection<int> Verges;
        public ICollection<int> Parents;        
    }
    public class SBRep_Face : SBRep_Primitive
    {
        public int GroupID = -1;
        public Vector3d Normal;
        public int OutsideLoop;
        public ICollection<int> InsideLoops;
    }


    public class LoopEdge : IIndexed
    {
        public int ID { get; set; } = -1;
        public ICollection<int> edgeIDs;
        public Index2i neigbor;

        public LoopEdge()
        {
            edgeIDs = new List<int>();
        }
        public LoopEdge(ICollection<int> edgeIDs, Index2i neigbor) 
        {
            this.neigbor = neigbor;
            this.edgeIDs = edgeIDs;

        }
    }

    /// <summary>
    /// Список элементов IIndexed с автоматической расстановкой ID и доступом по ID
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IndexedCollection<T> :
        ICollection<T>
        where T : IIndexed
    {
        private const int undefindedID = -1;
        private int _maxIndex = undefindedID;
        private Dictionary<int, T> map;
        public int Count => map.Count;

        public bool IsReadOnly => false;

        public T this[int i]
        {
            get
            {
                return map[i];
            }
            set
            {
                map[i] = value;
            }
        }

        public IndexedCollection()
        {
            map = new Dictionary<int, T>();
        }

        private int GetNextIndex()
        {
            ++_maxIndex;
            return _maxIndex;
        }


        public void Add(T item)
        {
            if (item.ID == undefindedID)
                item.ID = GetNextIndex();
            else
                _maxIndex = Math.Max(item.ID, _maxIndex);
            map.Add(item.ID, item);
        }

        public void Clear()
        {
            map.Clear();
        }

        public bool Contains(T item)
        {
            return map.ContainsKey(item.ID);
        }
        public bool ContainsKey(int key)
        {
            return map.ContainsKey(key);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return map.Values.GetEnumerator();
        }

        public bool Remove(T item)
        {
            return map.Remove(item.ID);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return map.Values.GetEnumerator();
        }

        public IEnumerable<int> GetIndexes()
        {
            return map.Keys;
        }
    }
}
