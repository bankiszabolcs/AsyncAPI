export interface CommentUser {
  id: string;
  name: string;
  avatarUrl?: string | null;
}

export interface Comment {
  id: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  user: CommentUser;
  parentCommentId: string | null;
  replies: Comment[];
}
