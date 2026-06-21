using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record AddVideoToPlaylistRequest([property: Required] Guid VideoId);
