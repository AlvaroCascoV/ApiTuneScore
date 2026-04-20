using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NugetTuneScore.Constants;
using NugetTuneScore.Models;

namespace ApiTuneScore.Data;

public partial class TuneScoreContext : DbContext
{
    public TuneScoreContext(DbContextOptions<TuneScoreContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Album> Albums { get; set; } = null!;
    public virtual DbSet<Artist> Artists { get; set; } = null!;
    public virtual DbSet<Genre> Genres { get; set; } = null!;
    public virtual DbSet<Rating> Ratings { get; set; } = null!;
    public virtual DbSet<Playlist> Playlists { get; set; } = null!;
    public virtual DbSet<PlaylistSong> PlaylistSongs { get; set; } = null!;
    public virtual DbSet<CityLocation> CityLocations { get; set; } = null!;
    public virtual DbSet<Song> Songs { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<V_UserLogin> V_UserLogin { get; set; } = null!;
    public virtual DbSet<UserSalt> UserSalts { get; set; } = null!;
    public virtual DbSet<SongListItemDto> SongListItems { get; set; } = null!;
    public virtual DbSet<AlbumListItemDto> AlbumListItems { get; set; } = null!;
    public virtual DbSet<ArtistListItemDto> ArtistListItems { get; set; } = null!;
    public virtual DbSet<SongComment> SongComments { get; set; } = null!;
    public virtual DbSet<SongCommentVote> SongCommentVotes { get; set; } = null!;
    public virtual DbSet<ArtistLinkRequest> ArtistLinkRequests { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configuration is provided via dependency injection in Program.cs.
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        RegisterSequences(modelBuilder);

        modelBuilder.Entity<Album>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Albums__3214EC07B8837BA3");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Albums}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ContentStatus).HasDefaultValue(ContentStatuses.Approved);
            entity.Property(e => e.DeleteRequested).HasDefaultValue(false);
            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasConstraintName("FK_Albums_Artists");
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Artists__3214EC078C1BB6CD");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Artists}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue(ArtistStatuses.Active);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Genres__3214EC07A1B5F34D");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Genres}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Ratings__3214EC076FB0DF19");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Ratings}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.Song).WithMany(p => p.Ratings).HasConstraintName("FK_Ratings_Songs");
            entity.HasOne(d => d.User).WithMany(p => p.Ratings).HasConstraintName("FK_Ratings_Users");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Playlists");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Playlists}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.HasOne(d => d.User).WithMany(p => p.Playlists)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Playlists_Users");
        });

        modelBuilder.Entity<PlaylistSong>(entity =>
        {
            entity.HasKey(e => new { e.PlaylistId, e.SongId }).HasName("PK_PlaylistSongs");
            entity.HasOne(d => d.Playlist).WithMany(p => p.PlaylistSongs)
                .HasForeignKey(d => d.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlaylistSongs_Playlists");
            entity.HasOne(d => d.Song).WithMany(p => p.PlaylistSongs)
                .HasForeignKey(d => d.SongId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlaylistSongs_Songs");
        });

        modelBuilder.Entity<Song>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Songs__3214EC072AF1BF27");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Songs}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ContentStatus).HasDefaultValue(ContentStatuses.Approved);
            entity.Property(e => e.DeleteRequested).HasDefaultValue(false);
            entity.HasOne(d => d.Album).WithMany(p => p.Songs).HasConstraintName("FK_Songs_Albums");
            entity.HasOne(d => d.Genre).WithMany(p => p.Songs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Songs_Genres");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC079ABE2BA9");
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.Users}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Role).HasDefaultValue(Roles.User);
            entity.Property(e => e.IsDisabled).HasDefaultValue(false);
            entity.HasOne(e => e.LinkedArtist)
                .WithMany()
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_ArtistId");
        });

        modelBuilder.Entity<V_UserLogin>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("V_UserLogin");
        });

        modelBuilder.Entity<UserSalt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.UserSalts}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.HasOne(us => us.User)
                .WithOne(u => u.UserSalt)
                .HasForeignKey<UserSalt>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSalts_Users");
        });

        modelBuilder.Entity<SongListItemDto>().HasNoKey();
        modelBuilder.Entity<AlbumListItemDto>().HasNoKey();
        modelBuilder.Entity<ArtistListItemDto>().HasNoKey();

        modelBuilder.Entity<CityLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.CityLocations}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        modelBuilder.Entity<SongComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.SongComments}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Content).HasMaxLength(500);

            entity.HasOne(e => e.Song)
                .WithMany(s => s.SongComments)
                .HasForeignKey(e => e.SongId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SongComments_Songs");

            entity.HasOne(e => e.User)
                .WithMany(u => u.SongComments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_SongComments_Users");

            entity.HasOne(e => e.ParentComment)
                .WithMany(p => p.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SongComments_Parent");

            entity.HasOne(e => e.Rating)
                .WithOne(r => r.Comment)
                .HasForeignKey<SongComment>(e => e.RatingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SongComments_Ratings");
        });

        modelBuilder.Entity<SongCommentVote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.SongCommentVotes}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.HasIndex(e => new { e.SongCommentId, e.UserId })
                .IsUnique()
                .HasDatabaseName("UQ_SongCommentVotes_Comment_User");

            entity.HasOne(e => e.SongComment)
                .WithMany(c => c.Votes)
                .HasForeignKey(e => e.SongCommentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SongCommentVotes_SongComments");

            entity.HasOne(e => e.User)
                .WithMany(u => u.SongCommentVotes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_SongCommentVotes_Users");
        });

        modelBuilder.Entity<ArtistLinkRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasDefaultValueSql($"NEXT VALUE FOR {SequenceNames.ArtistLinkRequests}")
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue(RequestStatuses.Pending);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ArtistLinkRequests)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ArtistLinkRequests_Users");

            entity.HasOne(e => e.Artist)
                .WithMany(a => a.LinkRequests)
                .HasForeignKey(e => e.ArtistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ArtistLinkRequests_Artists");

            entity.HasOne(e => e.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ArtistLinkRequests_Admins");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    private static void RegisterSequences(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("Seq_Users", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_UserSalts", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Genres", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Artists", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Albums", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Songs", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Playlists", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_Ratings", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_CityLocations", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_SongComments", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_SongCommentVotes", "dbo").IncrementsBy(1);
        modelBuilder.HasSequence<int>("Seq_ArtistLinkRequests", "dbo").IncrementsBy(1);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
