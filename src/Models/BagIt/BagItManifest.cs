using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Models.BagIt;

public class BagItManifest
{
    private readonly SortedDictionary<string, BagItManifestItem> items = new(StringComparer.InvariantCulture);
    private readonly Dictionary<byte[], List<BagItManifestItem>> checksumToItems = new(ByteArrayComparer.Default);

    public static async Task<BagItManifest> Parse(Stream stream, CancellationToken cancellationToken)
    {
        var result = new BagItManifest();

        var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
        {
            int index = line.IndexOf(' ');
            string checksum = line[..index];
            string filePath = BagitHelpers.DecodeFilePath(line[(index + 1)..]);

            result.AddOrUpdateItem(new(filePath, Convert.FromHexString(checksum)));
        }

        return result;
    }

    public bool Contains(string filePath) => items.ContainsKey(filePath);

    public bool TryGetItem(string filePath, out BagItManifestItem item)
    {
        if (items.TryGetValue(filePath, out var value))
        {
            item = value;
            return true;
        }

        item = new("", []);
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

    public bool AddOrUpdateItem(BagItManifestItem item)
    {
        if (TryGetItem(item.FilePath, out var existingItem))
        {
            if (item == existingItem)
            {
                return false;
            }

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

        return true;
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

    public byte[] Serialize()
    {
        var values = Items.Select(i => 
            Convert.ToHexString(i.Checksum) + " " + 
            BagitHelpers.EncodeFilePath(i.FilePath));

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public IEnumerable<BagItManifestItem> Items => items.Values;
}
