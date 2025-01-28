using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage;

public interface IStorageService
{
    Task<BaseFileMetadata> StoreFile(string filePath, StreamWithLength data, string? contentType, CancellationToken cancellationToken);

    Task DeleteFile(string filePath, CancellationToken cancellationToken);

    Task<FileMetadata?> GetFileMetadata(string filePath, CancellationToken cancellationToken);

    Task<FileData?> GetFileData(string filePath, ByteRange? byteRange, CancellationToken cancellationToken);

    IAsyncEnumerable<FileMetadata> ListFiles(string path, CancellationToken cancellationToken);
}