namespace DatasetFileUpload.Services.Auth;

record AuthInfo(
    string? Email,
    string? EduPersonPrincipalName
)
{
    public bool IsEmpty => Email == null && EduPersonPrincipalName == null;
}

interface IAuthService
{
    public Task<AuthInfo> getAuthenticatedUser(HttpContext httpContext);
}