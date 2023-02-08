using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurfaceInterpolation.Tools.SBRep.Utils
{
    public interface IIndexed
    {
        int ID { get; set; }
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
            _maxIndex = undefindedID;
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
