using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Models.BagIt;

public class BagItManifest
{
    private readonly Dictionary<string, BagItManifestItem> items = [];
    private readonly Dictionary<byte[], List<BagItManifestItem>> checksumToItems = new(ByteArrayComparer.Default);

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
            string filePath = DecodePath(line[(index + 1)..]);

            result.AddOrUpdateItem(new()
            {
                Checksum = Convert.FromHexString(checksum),
                FilePath = filePath
            });
        }

        return result;
    }

    public bool TryGetItem(string filePath, out BagItManifestItem item)
    {
        if (items.TryGetValue(filePath, out var value))
        {
            item = value;
            return true;
        }

        item = new() { Checksum = [], FilePath = "" };
        return false;
    }

    public IEnumerable<BagItManifestItem> GetItemsByChecksum(byte[] checksum)
    {
        if (checksumToItems.TryGetValue(checksum, out var items))
        {
            return items;
        }

        return [];
    }

    public void AddOrUpdateItem(BagItManifestItem item)
    {
        if (TryGetItem(item.FilePath, out var existingItem))
        {
            checksumToItems[existingItem.Checksum].Remove(existingItem);
        }

        items[item.FilePath] = item;

        if (checksumToItems.TryGetValue(item.Checksum, out var values))
        {
            values.Add(item);
        }
        else
        {
            checksumToItems[item.Checksum] = [item];
        }
    }

    public bool RemoveItem(string filePath)
    {
        if (TryGetItem(filePath, out var item))
        {
            if (checksumToItems.TryGetValue(item.Checksum, out var values))
            {
                values.Remove(item);
            }

            items.Remove(filePath);

            return true;
        }

        return false;
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

        var values = items.Select(i => Convert.ToHexString(i.Value.Checksum) + " " + EncodePath(i.Value.FilePath));

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public IEnumerable<BagItManifestItem> Items => items.Values;
}
