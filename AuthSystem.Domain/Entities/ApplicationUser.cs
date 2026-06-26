using Microsoft.AspNetCore.Identity;

namespace AuthSystem.Domain.Entities;
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
