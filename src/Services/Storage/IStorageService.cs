using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public interface IStorageService
{
    Task<StorageServiceFileBase> StoreFile(string filePath, StreamWithLength data, string? contentType);

    Task DeleteFile(string filePath);

    Task<StreamWithLength?> GetFileData(string filePath);

    IAsyncEnumerable<StorageServiceFile> ListFiles(string path);
}