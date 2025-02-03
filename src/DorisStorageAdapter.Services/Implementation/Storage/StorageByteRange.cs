using System.Globalization;

namespace DorisStorageAdapter.Services.Implementation.Storage;

internal sealed record StorageByteRange(
    long? From,
    long? To)
{
    public string ToHttpRangeValue() =>
        string.Format(CultureInfo.InvariantCulture, "bytes={0}-{1}", From, To);
}