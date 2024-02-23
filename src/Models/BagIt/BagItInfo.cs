using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatasetFileUpload.Models.BagIt;

public class BagItInfo
{
    private readonly Dictionary<string, List<BagItInfoItem>> items = [];

    private const string baggingDateLabel = "Bagging-Date";
    private const string bagSizeLabel = "Bag-Size";
    private const string externalIdentifierLabel = "External-Identifier";
    private const string bagGroupIdentifierLabel = "Bag-Group-Identifier";
    private const string payloadOxumLabel = "Payload-Oxum";

    private const string accessLevelLabel = "Access-Level";
    private const string withdrawnLabel = "Withdrawn";

    private const string openAccessValue = "info:eu-repo/semantics/openAccess";
    private const string restrictedAccessValue = "info:eu-repo/semantics/restrictedAccess";

    public DateTime? BaggingDate
    {
        get => GetValue(baggingDateLabel, v => DateTime.TryParseExact(v,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateTime) ? dateTime : (DateTime?)null);

        set => SetOrRemoveItem(baggingDateLabel, value?.ToString("yyyy-MM-dd"));
    }

    public string? BagSize
    {
        get => GetValue(bagSizeLabel, v => v);
        set => SetOrRemoveItem(bagSizeLabel, value);
    }

    public string? ExternalIdentifier
    {
        get => GetValue(externalIdentifierLabel, v => v);
        set => SetOrRemoveItem(externalIdentifierLabel, value);
    }

    public string? BagGroupIdentifier
    {
        get => GetValue(bagGroupIdentifierLabel, v => v);
        set => SetOrRemoveItem(bagGroupIdentifierLabel, value);
    }

    public PayloadOxumType? PayloadOxum
    {
        get => GetValue(payloadOxumLabel, v =>
        {
            var values = v.Split('.');
            if (values.Length == 2 &&
                long.TryParse(values[0], out long octetCount) &&
                long.TryParse(values[1], out long streamCount))
            {
                return new PayloadOxumType(octetCount, streamCount);
            }

            return null;
        });

        set => SetOrRemoveItem(payloadOxumLabel, value == null ? null : 
            value.OctetCount.ToString() + '.' + value.StreamCount.ToString());
    }

    public AccessLevelEnum? AccessLevel
    {
        get => GetValue(accessLevelLabel, v => v switch
        {
            openAccessValue => AccessLevelEnum.open,
            restrictedAccessValue => AccessLevelEnum.restricted,
            _ => (AccessLevelEnum?)null
        });

        set => SetOrRemoveItem(accessLevelLabel, value switch
        {
            AccessLevelEnum.open => openAccessValue,
            AccessLevelEnum.restricted => restrictedAccessValue,
            _ => null
        });
    }

    public bool? Withdrawn
    {
        get => GetValue(withdrawnLabel, v => bool.TryParse(v, out bool value) ? value : (bool?)null);
        set => SetOrRemoveItem(withdrawnLabel, value?.ToString());
    }

    private void SetOrRemoveItem(string label, string? value)
    {
        if (value == null)
        {
            items.Remove(label);
        }
        else
        {
            items[label] = [new(label, value)];
        }
    }

    private T? GetValue<T>(string label, Func<string, T?> converter)
    {
        if (items.TryGetValue(label, out var item))
        {
            var result = converter(item.First().Value);

            if (result != null)
            {
                return result;
            }
        }

        return default;
    }

    public static async Task<BagItInfo> Parse(Stream stream)
    {
        var result = new BagItInfo();

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        string value = "";
        string label = "";
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                // value is continued from previous line
                value += " " + line.TrimStart();
            }
            else
            {
                if (value != "")
                {
                    var item = new BagItInfoItem(label, value);
                    if (result.items.TryGetValue(label, out var existing))
                    {
                        existing.Add(item);
                    }
                    else
                    {
                        result.items[label] = [item];
                    }
                }
                int index = line.IndexOf(": ");
                label = line[..index];
                value = line[(index + 2)..];
            }
        }

        return result;
    }


    // Maybe serialize to stream here instead?
    public byte[] Serialize()
    {
        var values = items.Values.SelectMany(i => i).Select(i => i.Label + ": " + i.Value);

        return Encoding.UTF8.GetBytes(string.Join("\n", values));
    }

    public record PayloadOxumType(long OctetCount, long StreamCount);

    public enum AccessLevelEnum
    { 
        open, 
        restricted 
    };
}
