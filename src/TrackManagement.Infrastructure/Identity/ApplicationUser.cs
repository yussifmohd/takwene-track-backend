using Microsoft.AspNetCore.Identity;

namespace TrackManagement.Infrastructure.Identity;

/// <summary>Identity user with Guid keys. Only the seeded admin exists (no registration).</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}
