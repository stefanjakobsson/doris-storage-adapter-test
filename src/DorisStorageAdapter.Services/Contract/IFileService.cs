using DorisStorageAdapter.Services.Contract.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Contract;

public interface IFileService
{
    Task<FileMetadata> StoreFile(
        DatasetVersion datasetVersion,
        FileType type,
        string filePath,
        Stream data,
        long size,
        string? contentType,
        CancellationToken cancellationToken);

    Task DeleteFile(
        DatasetVersion datasetVersion,
        FileType type,
        string filePath,
        CancellationToken cancellationToken);

    Task ImportFiles(
        DatasetVersion datasetVersion,
        FileType type,
        string fromVersion,
        CancellationToken cancellationToken);

    Task<FileData?> GetFileData(
        DatasetVersion datasetVersion,
        FileType type,
        string filePath,
        bool isHeadRequest,
        ByteRange? byteRange,
        bool restrictToPubliclyAccessible,
        CancellationToken cancellationToken);

    IAsyncEnumerable<FileMetadata> ListFiles(
        DatasetVersion datasetVersion,
        CancellationToken cancellationToken);

    Task WriteFileDataAsZip(
        DatasetVersion datasetVersion,
        string[] paths,
        Stream stream,
        CancellationToken cancellationToken);
}
