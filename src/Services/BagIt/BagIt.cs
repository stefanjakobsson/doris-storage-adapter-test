using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DorisStorageAdapter.Services.BagIt;

internal sealed class BagIt : IBagItElement<BagIt>
{
    private static readonly byte[] contents = Encoding.UTF8.GetBytes("BagIt-Version: 1.0\nTag-File-Character-Encoding: UTF-8\n");
    public static readonly BagIt Instance = new();

    private BagIt() { }

    public static string FileName => "bagit.txt";

    public static Task<BagIt> Parse(Stream stream, CancellationToken cancellationToken)
    {
        return Task.FromResult(Instance);
    }

    public bool HasValues() => true;

    public byte[] Serialize() => contents;
}
