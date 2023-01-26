namespace DatasetFileUpload.Services.Auth;

record AuthInfo(
    string? email,
    string? eduPersonPrincipalName
)
{
    public bool IsEmpty => email == null && eduPersonPrincipalName == null;
}

interface IAuthService
{
    public Task<AuthInfo> getAuthenticatedUser(HttpContext httpContext);
}