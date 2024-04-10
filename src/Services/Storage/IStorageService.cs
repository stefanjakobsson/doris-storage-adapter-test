using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public interface IStorageService
{
    Task<RoCrateFile> StoreFile(string filePath, StreamWithLength data);

    Task DeleteFile(string filePath);

    Task<bool> FileExists(string filePath);

    Task<StreamWithLength?> GetFileData(string filePath);

    IAsyncEnumerable<RoCrateFile> ListFiles(string path);
}