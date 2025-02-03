namespace DorisStorageAdapter.Services.Implementation.BagIt.Manifest;

internal sealed class BagItPayloadManifest : BagItManifest<BagItPayloadManifest>, IBagItElement<BagItPayloadManifest>
{
    public static string FileName => "manifest-sha256.txt";
}
