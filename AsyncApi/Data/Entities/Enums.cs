namespace AsyncApi.Enums;

// Segéd enumok — az integer értékek a DB szótártáblák ID-jaival korrelálnak
// Összehasonlításnál: video.StatusId == (int)ProcessingStatus.Completed
public enum ProcessingStatus
{
    Queued     = 1,
    Processing = 2,
    Completed  = 3,
    Failed     = 4
}

public enum Visibility
{
    Public   = 1,
    Unlisted = 2,
    Private  = 3
}

public enum ReactionType
{
    Like    = 1,
    Dislike = 2
}
