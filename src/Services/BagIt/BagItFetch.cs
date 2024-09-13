using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.BagIt;

internal sealed class BagItFetch
{
    private readonly SortedDictionary<string, BagItFetchItem> items = new(StringComparer.Ordinal);

    public static async Task<BagItFetch> Parse(Stream stream, CancellationToken cancellationToken)
    {
        var result = new BagItFetch();

        var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
        {
            int index1 = line.IndexOf(' ', StringComparison.Ordinal);
            string url = line[..index1];
            string remaining = line[(index1 + 1)..];
            int index2 = remaining.IndexOf(' ', StringComparison.Ordinal);
            long? length = long.TryParse(remaining[..index2], out long value) ? value : null;
            string filePath = BagitHelpers.DecodeFilePath(remaining[(index2 + 1)..]);

            result.AddOrUpdateItem(new(filePath, length, url));
        }

        return result;
    }

    public bool Contains(string filePath) => items.ContainsKey(filePath);

    public bool TryGetItem(string filePath, out BagItFetchItem item)
    {
        if (items.TryGetValue(filePath, out var value))
        {
            item = value;
            return true;
        }

        item = new("", 0, "");
        return false;
    }

    public bool AddOrUpdateItem(BagItFetchItem item)
    {
        if (TryGetItem(item.FilePath, out var existingItem) &&
            item == existingItem)
        {
            return false;
        }

        items[item.FilePath] = item;

        return true;
    }

    public bool RemoveItem(string filePath) => items.Remove(filePath);

    public byte[] Serialize()
    {
        var values = Items.Select(i =>
            i.Url + ' ' +
            (i.Length?.ToString(CultureInfo.InvariantCulture) ?? "-") + ' ' +
            BagitHelpers.EncodeFilePath(i.FilePath));

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public IEnumerable<BagItFetchItem> Items => items.Values;
}
