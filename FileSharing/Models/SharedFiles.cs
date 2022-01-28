using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.IO;
using System.Diagnostics;
using FileSharing.Utils;

namespace FileSharing.Models
{
    public class SharedFiles : IEnumerable<SharedFile>
    {
        private readonly ConcurrentDictionary<long, SharedFile> _files;
        private readonly Indexer _indexer;

        public SharedFiles()
        {
            _files = new ConcurrentDictionary<long, SharedFile>();
            _indexer = new Indexer();
        }

        public event EventHandler<EventArgs>? SharedFileAdded;
        public event EventHandler<EventArgs>? SharedFileChanged;
        public event EventHandler<EventArgs>? SharedFileRemoved;

        public SharedFile this[long index]
        {
            get => _files[index];
            private set => _files[index] = value;
        }

        public IEnumerable<SharedFile> SharedFilesList => _files.Values;

        public bool HasFile(long index)
        {
            return _files.ContainsKey(index);
        }

        public bool HasFile(string fileHash)
        {
            return _files.Values.Any(sharedFile => sharedFile.Hash == fileHash);
        }

        public bool HasFileAvailable(string fileHash)
        {
            return _files.Values.Any(
                sharedFile => sharedFile.Hash == fileHash &&
                sharedFile.IsHashCalculated &&
                !sharedFile.IsCorrupted);
        }

        public SharedFile GetByHash(string fileHash)
        {
            return _files.Values.First(sharedFile => sharedFile.Hash == fileHash);
        }

        public List<FileInfo> GetAvailableFiles()
        {
            return _files.Values.
                Where(sharedFile => sharedFile.IsHashCalculated).
                Select(sharedFile => new FileInfo(sharedFile)).
                ToList();
        }

        public void AddFile(string filePath)
        {
            var hashComputingTask = new Task(() => AddFileRoutine(filePath));
            hashComputingTask.Start();
        }

        private void AddFileRoutine(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine("(AddFileRoutine) File doesn't exist");
                    return;
                }

                var name = Path.GetFileName(filePath);
                using var fileStream = File.OpenRead(filePath);
                var size = fileStream.Length;

                var sharedFile = new SharedFile(_indexer.GetIndex(), name, size, filePath);
                sharedFile.HashCalculated += OnSharedFileHashCalculated;

                if (_files.TryAdd(sharedFile.Index, sharedFile))
                {
                    SharedFileAdded?.Invoke(this, EventArgs.Empty);

                    Debug.WriteLine("(AddFileRoutine) File " + sharedFile.Name + " added to collection of files");

                    _files[sharedFile.Index].ComputeHashOfFile();

                    Debug.WriteLine("(AddFileRoutine) Hash for file " + _files[sharedFile.Index].Name + " has been calculated: " + _files[sharedFile.Index].Hash);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("(AddFileRoutine) " + e);
            }
        }

        private void OnSharedFileHashCalculated(object? sender, EventArgs e)
        {
            SharedFileChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveFile(long fileIndex)
        {
            if (HasFile(fileIndex))
            {
                if (_files.TryRemove(fileIndex, out SharedFile? removedFile) &&
                    removedFile != null)
                {
                    removedFile.CloseStream();

                    SharedFileRemoved?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void CloseAllFileStreams()
        {
            foreach (var sharedFile in _files.Values)
            {
                sharedFile.CloseStream();
            }
        }

        public IEnumerator<SharedFile> GetEnumerator()
        {
            return _files.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_files.Values).GetEnumerator();
        }
    }
}
