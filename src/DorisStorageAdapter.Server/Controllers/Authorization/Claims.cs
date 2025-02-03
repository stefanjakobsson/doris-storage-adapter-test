using DorisStorageAdapter.Services.Contract.Models;
using System.Collections.Generic;
using System.Security.Claims;

namespace DorisStorageAdapter.Server.Controllers.Authorization;

internal static class Claims
{
    public const string DatasetIdentifier = "dataset_identifier";
    public const string DatasetVersion = "dataset_version";

    public static bool CheckClaims(DatasetVersion datasetVersion, IEnumerable<Claim> claims)
    {
        bool identifierFound = false;
        bool versionFound = false;

        foreach (var claim in claims)
        {
            if (claim.Type == DatasetIdentifier)
            {
                if (claim.Value == datasetVersion.Identifier)
                {
                    identifierFound = true;
                }
                else
                {
                    return false;
                }
            }
            else if (claim.Type == DatasetVersion)
            {
                if (claim.Value == datasetVersion.Version)
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
