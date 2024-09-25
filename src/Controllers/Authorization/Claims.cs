using DorisStorageAdapter.Models;
using System.Collections.Generic;
using System.Security.Claims;

namespace DorisStorageAdapter.Controllers.Authorization;

internal static class Claims
{
    public const string DatasetIdentifier = "dataset_identifier";
    public const string DatasetVersionNumber = "dataset_version_number";

    public static bool CheckClaims(DatasetVersionIdentifier datasetVersion, IEnumerable<Claim> claims)
    {
        bool identifierFound = false;
        bool versionFound = false;

        foreach (var claim in claims)
        {
            if (claim.Type == DatasetIdentifier)
            {
                if (claim.Value == datasetVersion.DatasetIdentifier)
                {
                    identifierFound = true;
                }
                else
                {
                    return false;
                }
            }
            else if (claim.Type == DatasetVersionNumber)
            {
                if (claim.Value == datasetVersion.VersionNumber)
                {
                    versionFound = true;
                }
                else
                {
                    return false;
                }
            }

            if (identifierFound && versionFound)
            {
                return true;
            }
        }

        return false;
    }

}
