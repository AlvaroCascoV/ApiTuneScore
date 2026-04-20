namespace ApiTuneScore.Data;

/// <summary>SQL Server sequence names (dbo.Seq_* with NO CACHE). Documented in TuneScoreDB.sql header.</summary>
public static class SequenceNames
{
    public const string Users = "dbo.Seq_Users";
    public const string UserSalts = "dbo.Seq_UserSalts";
    public const string Genres = "dbo.Seq_Genres";
    public const string Artists = "dbo.Seq_Artists";
    public const string Albums = "dbo.Seq_Albums";
    public const string Songs = "dbo.Seq_Songs";
    public const string Playlists = "dbo.Seq_Playlists";
    public const string Ratings = "dbo.Seq_Ratings";
    public const string CityLocations = "dbo.Seq_CityLocations";
    public const string SongComments = "dbo.Seq_SongComments";
    public const string SongCommentVotes = "dbo.Seq_SongCommentVotes";
    public const string ArtistLinkRequests = "dbo.Seq_ArtistLinkRequests";
}
