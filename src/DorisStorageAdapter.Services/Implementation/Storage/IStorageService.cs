using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal interface IStorageService
{
    Task<StorageFileBaseMetadata> Store(string filePath, Stream data, long size, string? contentType, CancellationToken cancellationToken);

    Task Delete(string filePath, CancellationToken cancellationToken);

    Task<StorageFileMetadata?> GetMetadata(string filePath, CancellationToken cancellationToken);

    Task<StorageFileData?> GetData(string filePath, StorageByteRange? byteRange, CancellationToken cancellationToken);

    IAsyncEnumerable<StorageFileMetadata> List(string path, CancellationToken cancellationToken);
}