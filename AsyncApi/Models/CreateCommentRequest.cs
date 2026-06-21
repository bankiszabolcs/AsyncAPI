using System.ComponentModel.DataAnnotations;

namespace AsyncApi.Models;

public sealed record CreateCommentRequest([MaxLength(2000)] string Content, Guid? ParentCommentId);
