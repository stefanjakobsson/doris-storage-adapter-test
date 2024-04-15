namespace DatasetFileUpload.Authorization;

internal record ServiceScope : Scope
{
    private ServiceScope(string name) : base(name) { }

    public static ServiceScope Scope = new(service);
}
