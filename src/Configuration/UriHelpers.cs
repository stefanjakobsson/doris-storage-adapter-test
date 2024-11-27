using System;

namespace DorisStorageAdapter.Configuration;

internal static class UriHelpers
{
    public static Uri EnsureUriEndsWithSlash(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.AbsoluteUri.EndsWith('/'))
        {
            return new Uri(uri.OriginalString + '/');
        }

        return uri;
    }
}
