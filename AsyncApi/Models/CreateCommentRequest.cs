using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record CreateCommentRequest([property: MaxLength(2000)] string Content, Guid? ParentCommentId);
