namespace AsyncApi.Models;

// Videó metaadat-szerkesztés kérés törzse (PUT /videos/{id})
public sealed record UpdateVideoRequest(string Title, string? Description, int VisibilityId);
