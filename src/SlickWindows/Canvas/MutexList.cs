using System.Collections;

namespace SlickWindows.Canvas;

internal class MutexList<T> : ICollection<T>
{
    private readonly object  _lock = new();
    private readonly List<T> _data = new();

    public IEnumerator<T> GetEnumerator()
    {
        List<T> backing;
        lock (_lock)
        {
            backing = new List<T>(_data.ToArray());
        }

        return backing.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _data.Add(item);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _data.Clear();
        }
    }

    public bool Contains(T item)
    {
        lock (_lock)
        {
            return _data.Contains(item);
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (_lock)
        {
            _data.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(T item)
    {
        lock (_lock)
        {
            return _data.Remove(item);
        }
    }

    public int Count {
        get {
            lock (_lock)
            {
                return _data.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public T this[int idx]
    {
        get
        {
            T item;
            lock (_lock)
            {
                item = _data[idx];
            }
            return item;
        }
    }
}