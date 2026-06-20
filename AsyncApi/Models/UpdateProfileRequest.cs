using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record UpdateProfileRequest([property: MaxLength(50)] string? DisplayName, Guid? AvatarImageId);
