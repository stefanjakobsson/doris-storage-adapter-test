using System;

namespace DorisStorageAdapter.Helpers;

public static class UriHelpers
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
