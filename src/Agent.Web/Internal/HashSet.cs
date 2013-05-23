using System;
using System.Collections.Generic;
using System.Text;

namespace Gibraltar.Agent.Web.Internal
{
    using System.Collections;

    internal class HashSet<T> : IEnumerable<T>
    {
        private readonly Dictionary<T, T> m_Dict;

        public HashSet(IEqualityComparer<T> comparer)
        {
            m_Dict = new Dictionary<T, T>(comparer);
        }

        public HashSet(IEnumerable<T> source)
        {
            m_Dict = new Dictionary<T, T>();
            foreach (var item in source)
            {
                m_Dict.Add(item, item);
            }
        }

        public bool Contains(T item)
        {
            return m_Dict.ContainsKey(item);
        }

        public bool Add(T item)
        {
            if (m_Dict.ContainsKey(item))
                return false;
            m_Dict.Add(item, item);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Dict.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
