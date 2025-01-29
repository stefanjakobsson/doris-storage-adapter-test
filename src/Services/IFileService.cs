using DorisStorageAdapter.Models;
using DorisStorageAdapter.Services.Storage;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services;

public interface IFileService
{
    Task<Models.File> StoreFile(
        DatasetVersion datasetVersion,
        FileType type,
        string filePath,
        StreamWithLength data,
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

    IAsyncEnumerable<Models.File> ListFiles(
        DatasetVersion datasetVersion,
        CancellationToken cancellationToken);

    Task WriteFileDataAsZip(
        DatasetVersion datasetVersion,
        string[] paths,
        Stream stream,
        CancellationToken cancellationToken);
}
