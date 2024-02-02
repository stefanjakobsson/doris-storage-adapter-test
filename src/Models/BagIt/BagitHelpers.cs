namespace DatasetFileUpload.Models.BagIt;

internal static class BagitHelpers
{
    public static string EncodeFilePath(string filePath) =>
        filePath
            .Replace("%", "%25")
            .Replace("\n", "%0A")
            .Replace("\r", "%0D");

    public static string DecodeFilePath(string filePath) =>
          filePath
              .Replace("%25", "%")
              .Replace("%0A", "\n")
              .Replace("%0a", "\n")
              .Replace("%0D", "\r")
              .Replace("%0d", "\r");
}
