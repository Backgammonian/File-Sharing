using System;
using System.Collections.Generic;
using System.Text;

namespace FileSharing.Utils
{
    public class SimpleWriter
    {
        private readonly List<byte[]> _data;
        private long _length;

        public SimpleWriter()
        {
            _data = new List<byte[]>();
            _length = 0;
        }

        public long Length => _length;

        public void Put(byte value)
        {
            _data.Add(new byte[] { value });
            _length += 1;
        }

        public void Put(int value)
        {
            var array = BitConverter.GetBytes(value);
            _data.Add(array);
            _length += array.Length;
        }

        public void Put(uint value)
        {
            var array = BitConverter.GetBytes(value);
            _data.Add(array);
            _length += array.Length;
        }

        public void Put(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var length = Encoding.UTF8.GetByteCount(value);
            Put(length);

            var bytes = Encoding.UTF8.GetBytes(value);
            _data.Add(bytes);
            _length += bytes.Length;
        }

        public void Put(byte[] value)
        {
            _data.Add(value);
            _length += value.Length;
        }

        public byte[] Get()
        {
            var result = new byte[_length];
            var position = 0;
            for (int i = 0; i < _data.Count; i++)
            {
                Buffer.BlockCopy(_data[i], 0, result, position, _data[i].Length);
                position += _data[i].Length;
            }

            _data.Clear();
            _length = 0;

            return result;
        }
    }
}
