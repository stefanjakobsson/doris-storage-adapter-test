using System;
using System.IO;
using System.Threading.Tasks;

namespace DatasetFileUpload.Services.Storage;

public record StreamWithLength(Stream Stream, long Length) : IDisposable, IAsyncDisposable
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
