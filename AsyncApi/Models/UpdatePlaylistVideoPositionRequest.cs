using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public record UpdatePlaylistVideoPositionRequest([property: Required, Range(1, int.MaxValue)] int Position);
