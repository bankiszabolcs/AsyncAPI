using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record UpdateProfileRequest([MaxLength(50)] string? DisplayName, Guid? AvatarImageId);
