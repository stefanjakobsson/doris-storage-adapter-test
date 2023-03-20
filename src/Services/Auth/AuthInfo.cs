public record AuthInfo(
    string? Email,
    string? EduPersonPrincipalName
)
{
    public bool IsEmpty => Email == null && EduPersonPrincipalName == null;
}