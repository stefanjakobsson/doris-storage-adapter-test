namespace DatasetFileUpload.Authorization;

internal record DataAccessScope : Scope
{
    private DataAccessScope(string name) : base(name) { }

    public static DataAccessScope Read = new(readData);
    public static DataAccessScope Write = new(writeData);
}