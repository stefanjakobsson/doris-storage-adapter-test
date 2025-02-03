using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal interface IStorageService
{
    Task<StorageFileBaseMetadata> StoreFile(string filePath, Stream data, long size, string? contentType, CancellationToken cancellationToken);

    Task DeleteFile(string filePath, CancellationToken cancellationToken);

    Task<StorageFileMetadata?> GetFileMetadata(string filePath, CancellationToken cancellationToken);

    Task<StorageFileData?> GetFileData(string filePath, StorageByteRange? byteRange, CancellationToken cancellationToken);

    IAsyncEnumerable<StorageFileMetadata> ListFiles(string path, CancellationToken cancellationToken);
}