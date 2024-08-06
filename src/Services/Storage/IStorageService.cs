using System.Collections.Generic;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage;

public interface IStorageService
{
    Task<StorageServiceFileBase> StoreFile(string filePath, FileData data);

    Task DeleteFile(string filePath);

    Task<FileData?> GetFileData(string filePath);

    IAsyncEnumerable<StorageServiceFile> ListFiles(string path);
}