using DorisStorageAdapter.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services;

public interface IDatasetVersionService
{
    Task PublishDatasetVersion(
        DatasetVersion datasetVersion,
        AccessRight accessRight,
        string doi,
        CancellationToken cancellationToken);

    Task WithdrawDatasetVersion(
        DatasetVersion datasetVersion,
        CancellationToken cancellationToken);
}
