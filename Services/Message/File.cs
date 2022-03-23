using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public enum FileStatus
    {
        Pending,
        Success,
        Failed
    }

    public class File
    {
        // Slice file content, every subpackage has FILE_SUBPACKAGE_MODE bytes
        private const int FILE_SUBPACKEGE_MODE = 1023;
        private readonly List<byte> _content;
        public Semaphore Semaphore { get; } = new Semaphore(0, 1);
        public FileStatus FileStatus { get; set; }
        public Queue<byte[]> Packages { get; }
        public int PackageCount { get; }

        public File(List<byte> content)
        {
            // File status pending: wait for transmition
            FileStatus = FileStatus.Pending;
            // File content in bit
            _content = content;
            // Queue for file subpackages
            Packages = new Queue<byte[]>();
            // Need to slice file into several packages via FILE_SUBPACKAGE_MODE
            PackageCount = _content.Count / FILE_SUBPACKEGE_MODE + 1;

            for (int index = 0; index < PackageCount; index++)
            {
                // Assign a byte array to contain subpackage content, + 1 for CheckSum byte
                byte[] package = new byte[FILE_SUBPACKEGE_MODE + 1];
                // Has copied bytes
                int bytesCopied = index * FILE_SUBPACKEGE_MODE;
                int bytesRemain = _content.Count - bytesCopied;
                _content.CopyTo(bytesCopied, package, 0, bytesRemain > FILE_SUBPACKEGE_MODE ? FILE_SUBPACKEGE_MODE : bytesRemain);
                for (int i = 0; i < FILE_SUBPACKEGE_MODE; i++)
                {
                    package[^1] += package[i];
                }
                Packages.Enqueue(package);
            }
        }

        public void Success()
        {
            FileStatus = FileStatus.Success;
            Semaphore.Release();
        }

        public void Fail()
        {
            FileStatus = FileStatus.Failed;
            Semaphore.Release();
        }
    }
}
