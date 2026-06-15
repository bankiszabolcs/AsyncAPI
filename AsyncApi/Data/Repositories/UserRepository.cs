using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class UserRepository(AsyncApiDbContext db)
{
    public async Task<User?> GetByIdAsync(Guid id)
        => await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Active);

    // Első bejelentkezéskor létrehozza, utána frissíti az email/displayName mezőket
    public async Task<User> UpsertAsync(Guid id, string username, string email, string? displayName)
    {
        var user = await db.Users.FindAsync(id);

        if (user is null)
        {
            user = new User
            {
                Id           = id,
                Username     = username,
                Email        = email,
                DisplayName  = displayName,
                CreateDate   = DateTime.UtcNow,
                CreateUserId  = id,
                ModifyUserId   = id,
                Active       = true,
            };
            db.Users.Add(user);
        }
        else
        {
            //user.Email       = email;
            //user.DisplayName = displayName ?? user.DisplayName;
            //user.ModifyUserId = id;
        }

        await db.SaveChangesAsync();
        await db.Entry(user).Reference(u => u.AvatarImage).LoadAsync();
        return user;
    }

    public async Task<User?> UpdateProfileAsync(Guid userId, string? displayName, Guid? avatarImageId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Active);
        if (user is null) return null;

        if (displayName is not null) user.DisplayName = displayName;
        if (avatarImageId.HasValue)  user.AvatarImageId = avatarImageId;
        user.ModifyUserId = userId;
        user.ModifyDate   = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await db.Entry(user).Reference(u => u.AvatarImage).LoadAsync();
        return user;
    }
}
