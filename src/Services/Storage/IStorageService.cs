using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage;

public interface IStorageService
{
    Task<StorageServiceFileBase> StoreFile(string filePath, FileData data, CancellationToken cancellationToken);

    Task DeleteFile(string filePath, CancellationToken cancellationToken);

    Task<PartialFileData?> GetFileData(string filePath, ByteRange? byteRange, CancellationToken cancellationToken);

    IAsyncEnumerable<StorageServiceFile> ListFiles(string path, CancellationToken cancellationToken);
}