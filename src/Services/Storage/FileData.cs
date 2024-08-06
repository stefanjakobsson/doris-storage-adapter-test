using System;
using System.IO;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.Storage;

public record FileData(
    Stream Stream, 
    long Length,
    string? ContentType) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        Stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
