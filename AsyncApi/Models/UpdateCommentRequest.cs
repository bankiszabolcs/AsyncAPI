using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record UpdateCommentRequest([property: MaxLength(2000)] string Content);
