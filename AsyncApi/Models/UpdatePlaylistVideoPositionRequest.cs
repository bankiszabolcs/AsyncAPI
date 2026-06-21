using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record UpdatePlaylistVideoPositionRequest([Required, Range(1, int.MaxValue)] int Position);
