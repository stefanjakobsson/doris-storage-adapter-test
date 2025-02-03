using System;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal interface IStoragePathLock
{
    Task<IDisposable> LockPath(string path, CancellationToken cancellationToken);
}
