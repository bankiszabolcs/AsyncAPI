using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record UpdateCommentRequest([MaxLength(2000)] string Content);
