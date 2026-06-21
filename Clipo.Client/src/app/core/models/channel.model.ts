export interface Channel {
  id: string;
  displayName: string;
  avatarUrl: string | null;
  subscriberCount: number;
  isSubscribed: boolean | null;
}
