using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Models.BagIt;

public class BagItManifest
{
    private readonly Dictionary<string, byte[]> fileNameToChecksum = [];
    private readonly Dictionary<byte[], HashSet<string>> checksumToFileNames = new(ByteArrayComparer.Default);

    public static async Task<BagItManifest> Parse(Stream stream)
    {
        static string DecodePath(string path) =>
            path
                .Replace("%25", "%")
                .Replace("%0A", "\n")
                .Replace("%0a", "\n")
                .Replace("%0D", "\r")
                .Replace("%0d", "\r");

        var result = new BagItManifest();

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            int index = line.IndexOf(' ');
            string checksum = line[..index];
            string fileName = DecodePath(line[(index + 1)..]);

            result.SetChecksum(fileName, Convert.FromHexString(checksum));
        }

        return result;
    }

    public bool TryGetChecksum(string fileName, out byte[] checksum)
    {
        if (fileNameToChecksum.TryGetValue(fileName, out byte[]? value))
        {
            checksum = value;
            return true;
        }

        checksum = [];
        return false;
    }

    public bool TryGetFileNames(byte[] checksum, out IEnumerable<string> fileNames)
    {
        if (checksumToFileNames.TryGetValue(checksum, out HashSet<string>? value))
        {
            fileNames = value;
            return true;
        }

        fileNames = [];
        return false;
    }

    public void SetChecksum(string fileName, byte[] checksum)
    {
        if (TryGetChecksum(fileName, out byte[] oldChecksum))
        {
            checksumToFileNames[oldChecksum].Remove(fileName);
        }

        fileNameToChecksum[fileName] = checksum;

        if (checksumToFileNames.TryGetValue(checksum, out HashSet<string>? fileNames))
        {
            fileNames.Add(fileName);
        }
        else
        {
            checksumToFileNames[checksum] = [ fileName ];
        }
    }

    public void RemoveChecksum(string fileName)
    {
        if (TryGetChecksum(fileName, out byte[] checksum))
        {
            var files = checksumToFileNames[checksum];
            files.Remove(fileName);
            if (files.Count == 0)
            {
                checksumToFileNames.Remove(checksum);
            }
        }

        fileNameToChecksum.Remove(fileName);
    }

    // Maybe serialize to stream here instead?
    public byte[] Serialize()
    {
        static string EncodePath(string path)
        {
            return path
                .Replace("%", "%25")
                .Replace("\n", "%0A")
                .Replace("\r", "%0D");
        }

        var values = fileNameToChecksum.Select(k => Convert.ToHexString(k.Value) + " " + EncodePath(k.Key));

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public bool IsEmpty() => fileNameToChecksum.Count == 0;
}
