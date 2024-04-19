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

    private const string accessRightLabel = "Access-Right";
    private const string datasetStatusLabel = "Dataset-Status";

    // http://publications.europa.eu/resource/authority/access-right/PUBLIC
    private const string publicAccessRightValue = "PUBLIC";
    // http://publications.europa.eu/resource/authority/access-right/NON_PUBLIC
    private const string nonPublicAccessRightValue = "NON_PUBLIC";

    // http://publications.europa.eu/resource/authority/dataset-status/COMPLETED
    private const string completedDatasetStatusValue = "COMPLETED";
    // http://publications.europa.eu/resource/authority/dataset-status/WITHDRAWN
    private const string withdrawnDatasetStatusValue = "WITHDRAWN";

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

    public AccessRightEnum? AccessRight
    {
        get => GetValue(accessRightLabel, v => v switch
        {
            publicAccessRightValue => AccessRightEnum.@public,
            nonPublicAccessRightValue => AccessRightEnum.non_public,
            _ => (AccessRightEnum?)null
        });

        set => SetOrRemoveItem(accessRightLabel, value switch
        {
            AccessRightEnum.@public => publicAccessRightValue,
            AccessRightEnum.non_public => nonPublicAccessRightValue,
            _ => null
        });
    }

    public DatasetStatusEnum? DatasetStatus
    {
        get => GetValue(datasetStatusLabel, v => v switch
        {
            completedDatasetStatusValue => DatasetStatusEnum.completed,
            withdrawnDatasetStatusValue => DatasetStatusEnum.withdrawn,
            _ => (DatasetStatusEnum?)null
        });

        set => SetOrRemoveItem(datasetStatusLabel, value switch
        {
            DatasetStatusEnum.completed => completedDatasetStatusValue,
            DatasetStatusEnum.withdrawn => withdrawnDatasetStatusValue,
            _ => null
        });
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

        var reader = new StreamReader(stream, Encoding.UTF8);
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
}
