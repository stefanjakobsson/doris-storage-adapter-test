namespace DatasetFileUpload.Authorization;

internal abstract record Scope
{
    public const string service = "service";
    public const string readData = "read-data";
    public const string writeData = "write-data";

    public string Name { get; }

    protected Scope(string name) => Name = name;

    public static implicit operator string(Scope scope) => scope.Name;
}
