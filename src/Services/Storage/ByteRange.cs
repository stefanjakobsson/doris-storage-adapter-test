using System.Globalization;

namespace DorisStorageAdapter.Services.Storage;

public record ByteRange(
    long? From,
    long? To)
{
    public string ToHttpRangeValue() =>
        string.Format(CultureInfo.InvariantCulture, "bytes={0}-{1}", From, To);
}
