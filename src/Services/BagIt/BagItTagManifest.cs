namespace DorisStorageAdapter.Services.BagIt;

internal sealed class BagItTagManifest : BagItManifest<BagItTagManifest>, IBagItElement<BagItTagManifest>
{
    public static string FileName => "tagmanifest-sha256.txt";
}
