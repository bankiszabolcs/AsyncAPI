using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

// Videó metaadat-szerkesztés kérés törzse (PUT /videos/{id})
public sealed record UpdateVideoRequest(
    [MaxLength(100)] string Title,
    [MaxLength(5000)] string? Description,
    int VisibilityId,
    List<string>? Tags = null);
