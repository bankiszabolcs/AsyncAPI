using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

// Videó metaadat-szerkesztés kérés törzse (PUT /videos/{id})
public sealed record UpdateVideoRequest(
    [property: MaxLength(100)] string Title,
    [property: MaxLength(5000)] string? Description,
    int VisibilityId,
    List<string>? Tags = null);
