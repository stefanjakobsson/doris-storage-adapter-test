using DorisStorageAdapter.Services.Contract.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Contract;

public interface IDatasetVersionService
{
    Task PublishDatasetVersion(
        DatasetVersion datasetVersion, 
        AccessRight accessRight, 
        string canonicalDoi, 
        string doi, 
        CancellationToken cancellationToken);

    Task WithdrawDatasetVersion(
        DatasetVersion datasetVersion, 
        CancellationToken cancellationToken);
}
