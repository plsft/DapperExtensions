using Dapper;
using System.Collections.Generic;

namespace DapperExtensions
{
    public interface IMultipleResultReader
    {
        IEnumerable<T> Read<T>();
    }

    public class GridReaderResultReader : IMultipleResultReader
    {
        private readonly SqlMapper.GridReader _reader;

        public GridReaderResultReader(SqlMapper.GridReader reader)
        {
            _reader = reader;
        }

        public IEnumerable<T> Read<T>() => _reader.Read<T>();
    }

    public class SequenceReaderResultReader : IMultipleResultReader
    {
        private readonly Queue<SqlMapper.GridReader> _items;

        public SequenceReaderResultReader(IEnumerable<SqlMapper.GridReader> items)
        {
            _items = new(items);
        }

        public IEnumerable<T> Read<T>()
        {
            var reader = _items.Dequeue();
            return reader.Read<T>();
        }
    }
}