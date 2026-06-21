using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record AddVideoToPlaylistRequest([Required] Guid VideoId);
