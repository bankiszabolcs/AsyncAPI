export interface Playlist {
  id: string;
  title: string;
  description: string | null;
  visibilityId: number;
  videoCount: number;
  createdAt: string;
}

export interface PlaylistVideoItem {
  videoId: string;
  position: number;
  video: {
    id: string;
    title: string;
    duration: number;
    thumbnails: { width: number; url: string }[];
  };
}

export interface PlaylistDetail {
  id: string;
  title: string;
  description: string | null;
  visibilityId: number;
  createdAt: string;
  items: PlaylistVideoItem[];
}

export interface CreatePlaylistRequest {
  title: string;
  description?: string | null;
  visibilityId: number;
}

export interface UpdatePlaylistRequest {
  title: string;
  description?: string | null;
  visibilityId: number;
}
