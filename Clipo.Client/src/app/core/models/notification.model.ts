export type NotificationType = 'new_video' | 'new_comment' | 'comment_reply' | 'video_processed';

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  body: string | null;
  link: string | null;
  isRead: boolean;
  createDate: string;
}
