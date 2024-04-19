using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Models.BagIt;

public class BagItFetch
{
    private readonly Dictionary<string, BagItFetchItem> items = [];

    public static async Task<BagItFetch> Parse(Stream stream)
    {
        var result = new BagItFetch();

        var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            int index1 = line.IndexOf(' ');
            string url = line[..index1];
            string remaining = line[(index1 + 1)..];
            int index2 = remaining.IndexOf(' ');
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

    // Maybe serialize to stream here instead?
    public byte[] Serialize()
    {
        var values = Items.Select(i => 
            i.Url + ' ' + 
            (i.Length?.ToString() ?? "-") + ' ' +
            BagitHelpers.EncodeFilePath(i.FilePath));

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public IEnumerable<BagItFetchItem> Items => items.Values;
}
