using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record UpdatePlaylistRequest(
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(2000)] string? Description,
    int VisibilityId = 3
);
