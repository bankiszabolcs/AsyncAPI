using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ProcessingStatus = AsyncApi.Enums.ProcessingStatus;

namespace AsyncApi.Data.Repositories;

public class ImageRepository(AsyncApiDbContext db)
{
    public async Task<Image> CreateAsync(Guid id, string originalFileName, string extension, Guid userId)
    {
        var image = new Image
        {
            Id               = id,
            UserId           = userId,
            OriginalFileName = originalFileName,
            Extension        = extension,
            CreateUserId     = userId,
            ModifyUserId     = userId,
            CreateDate       = DateTime.UtcNow,
            Active           = true,
            Version          = 1
        };

        db.Images.Add(image);
        await db.SaveChangesAsync();

        return image;
    }

    public async Task UpdateStatusAsync(Guid id, ProcessingStatus status, int? width = null, int? height = null)
    {
        var image = await db.Images.FindAsync(id);
        if (image is null) return;
        image.StatusId = (int)status;
        if (width.HasValue)  image.Width  = width;
        if (height.HasValue) image.Height = height;
        await db.SaveChangesAsync();
    }

    public async Task<Image?> GetByIdAsync(Guid id)
    {
        return await db.Images
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id && i.Active);
    }
}
