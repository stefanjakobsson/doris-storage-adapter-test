using DatasetFileUpload.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public interface IStorageService
{
    public Task<RoCrateFile> StoreFile(string filePath, Stream stream);

    public Task DeleteFile(string filePath);

    public Task<StreamWithLength?> GetFileData(string filePath);

    public IAsyncEnumerable<RoCrateFile> ListFiles(string path);
}