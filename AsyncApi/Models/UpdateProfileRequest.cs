namespace AsyncApi.Models;

public sealed record UpdateProfileRequest(string? DisplayName, Guid? AvatarImageId);
