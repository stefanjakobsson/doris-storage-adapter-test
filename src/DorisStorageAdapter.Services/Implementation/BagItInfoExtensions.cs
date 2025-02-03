using DorisStorageAdapter.Services.Contract.Models;
using System.Linq;
using DorisStorageAdapter.Services.Implementation.BagIt.Info;

namespace DorisStorageAdapter.Services.Implementation;

internal static class BagItInfoExtensions
{
    private const string accessRightLabel = "Access-Right";
    private const string datasetStatusLabel = "Dataset-Status";
    private const string versionLabel = "Version";

    // http://publications.europa.eu/resource/authority/access-right/PUBLIC
    private const string publicAccessRightValue = "PUBLIC";
    // http://publications.europa.eu/resource/authority/access-right/NON_PUBLIC
    private const string nonPublicAccessRightValue = "NON_PUBLIC";

    // http://publications.europa.eu/resource/authority/dataset-status/COMPLETED
    private const string completedDatasetStatusValue = "COMPLETED";
    // http://publications.europa.eu/resource/authority/dataset-status/WITHDRAWN
    private const string withdrawnDatasetStatusValue = "WITHDRAWN";

    public static AccessRight? GetAccessRight(this BagItInfo bagItInfo) =>
        bagItInfo.GetCustomValues(accessRightLabel).FirstOrDefault() switch
        {
            publicAccessRightValue => AccessRight.@public,
            nonPublicAccessRightValue => AccessRight.nonPublic,
            _ => null
        };

    public static void SetAccessRight(this BagItInfo bagItInfo, AccessRight? accessRight) =>
        bagItInfo.SetCustomValues(accessRightLabel, accessRight switch
        {
            AccessRight.@public => [publicAccessRightValue],
            AccessRight.nonPublic => [nonPublicAccessRightValue],
            _ => []
        });


    public static DatasetStatus? GetDatasetStatus(this BagItInfo bagItInfo) =>
        bagItInfo.GetCustomValues(datasetStatusLabel).FirstOrDefault() switch
        {
            completedDatasetStatusValue => DatasetStatus.completed,
            withdrawnDatasetStatusValue => DatasetStatus.withdrawn,
            _ => null
        };

    public static void SetDatasetStatus(this BagItInfo bagItInfo, DatasetStatus? datasetStatus) =>
        bagItInfo.SetCustomValues(datasetStatusLabel, datasetStatus switch
        {
            DatasetStatus.completed => [completedDatasetStatusValue],
            DatasetStatus.withdrawn => [withdrawnDatasetStatusValue],
            _ => []
        });

    public static string? GetVersion(this BagItInfo bagItInfo) =>
        bagItInfo.GetCustomValues(versionLabel).FirstOrDefault();

    public static void SetVersion(this BagItInfo bagItInfo, string? version) =>
       bagItInfo.SetCustomValues(versionLabel, version == null ? [] : [version]);
}
