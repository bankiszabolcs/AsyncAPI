using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record UpdatePlaylistRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(2000)] string? Description,
    int VisibilityId = 3
);
