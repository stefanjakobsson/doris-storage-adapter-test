using System;

namespace DorisStorageAdapter.Services.Implementation.BagIt;

internal static class BagItPathEncoder
{
    public static string EncodeFilePath(string filePath) =>
        filePath
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal);

    public static string DecodeFilePath(string filePath) =>
        filePath
            .Replace("%25", "%", StringComparison.Ordinal)
            .Replace("%0A", "\n", StringComparison.Ordinal)
            .Replace("%0a", "\n", StringComparison.Ordinal)
            .Replace("%0D", "\r", StringComparison.Ordinal)
            .Replace("%0d", "\r", StringComparison.Ordinal);
}
