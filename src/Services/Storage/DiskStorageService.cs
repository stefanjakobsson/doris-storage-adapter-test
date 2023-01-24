using DatasetFileUpload.Services.Storage;
using DatasetFileUpload.Models;

class DiskStorageService : IStorageService{

     public bool storeManifest(string datasetIdentifier, string versionNumber, string manifest){
        throw new NotImplementedException();
     }

    public DatasetFileUpload.Models.File storeFile(string datasetIdentifier, string versionNumber, FileType type, IFormFile file){
        throw new NotImplementedException();
    }

    public bool deleteFile(string datasetIdentifier, string versionNumber, FileType type, string filePath){
        throw new NotImplementedException();
    }

}