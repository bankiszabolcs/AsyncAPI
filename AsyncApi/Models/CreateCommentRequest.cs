namespace AsyncApi.Models;

public sealed record CreateCommentRequest(string Content, Guid? ParentCommentId);
