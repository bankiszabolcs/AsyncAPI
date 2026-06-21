using System;
using System.Collections.Generic;
using AsyncApi.Data.Entities;
using AsyncApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data;

public partial class AsyncApiDbContext : DbContext
{
    public AsyncApiDbContext(DbContextOptions<AsyncApiDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditColumnLog> AuditColumnLogs { get; set; }

    public virtual DbSet<AuditSchemaLog> AuditSchemaLogs { get; set; }

    public virtual DbSet<AuditTable> AuditTables { get; set; }

    public virtual DbSet<AuditTableLog> AuditTableLogs { get; set; }

    public virtual DbSet<AuditTablesDependency> AuditTablesDependencies { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Image> Images { get; set; }

    public virtual DbSet<Playlist> Playlists { get; set; }

    public virtual DbSet<PlaylistVideo> PlaylistVideos { get; set; }

    public virtual DbSet<ProcessingStatus> ProcessingStatuses { get; set; }

    public virtual DbSet<ReactionType> ReactionTypes { get; set; }

    public virtual DbSet<SavedVideo> SavedVideos { get; set; }

    public virtual DbSet<RowLog> RowLogs { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<TableEventLog> TableEventLogs { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TransactionLog> TransactionLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Video> Videos { get; set; }

    public virtual DbSet<VideoReaction> VideoReactions { get; set; }

    public virtual DbSet<VideoTag> VideoTags { get; set; }

    public virtual DbSet<VideoView> VideoViews { get; set; }

    public virtual DbSet<Visibility> Visibilities { get; set; }

    public virtual DbSet<RelatedVideoResult> RelatedVideoResults { get; set; }

    public IQueryable<RelatedVideoResult> GetRelatedVideos(Guid videoId, Guid? userId, int limit)
        => FromExpression(() => GetRelatedVideos(videoId, userId, limit));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditColumnLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_column_log_pk");

            entity.ToTable("audit_column_log", "pgmemento", tb => tb.HasComment("Stores information about audited columns, which is important when restoring previous versions of tuples and tables"));

            entity.HasIndex(e => e.ColumnName, "column_log_column_idx");

            entity.HasIndex(e => e.TxidRange, "column_log_range_idx").HasMethod("gist");

            entity.HasIndex(e => e.AuditTableId, "column_log_table_idx");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('audit_column_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.AuditTableId)
                .HasComment("Foreign Key to pgmemento.audit_table_log")
                .HasColumnName("audit_table_id");
            entity.Property(e => e.ColumnDefault)
                .HasComment("The column's default expression")
                .HasColumnName("column_default");
            entity.Property(e => e.ColumnName)
                .HasComment("The name of the column")
                .HasColumnName("column_name");
            entity.Property(e => e.DataType)
                .HasComment("The column's data type (incl typemods)")
                .HasColumnName("data_type");
            entity.Property(e => e.NotNull)
                .HasComment("A flag to tell, if the column is a NOT NULL column or not")
                .HasColumnName("not_null");
            entity.Property(e => e.OrdinalPosition)
                .HasComment("The ordinal position within the table")
                .HasColumnName("ordinal_position");
            entity.Property(e => e.TxidRange)
                .HasComment("Stores the transaction IDs when the column has been created and dropped")
                .HasColumnName("txid_range");

            entity.HasOne(d => d.AuditTable).WithMany(p => p.AuditColumnLogs)
                .HasForeignKey(d => d.AuditTableId)
                .HasConstraintName("audit_column_log_fk");
        });

        modelBuilder.Entity<AuditSchemaLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_schema_log_pk");

            entity.ToTable("audit_schema_log", "pgmemento", tb => tb.HasComment("Stores information about how pgMemento is configured in audited database schema"));

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('audit_schema_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.DefaultAuditIdColumn)
                .HasComment("The default name for the audit_id column added to audited tables")
                .HasColumnName("default_audit_id_column");
            entity.Property(e => e.DefaultLogNewData)
                .HasDefaultValue(false)
                .HasComment("Default setting for tables to log new values")
                .HasColumnName("default_log_new_data");
            entity.Property(e => e.DefaultLogOldData)
                .HasDefaultValue(true)
                .HasComment("Default setting for tables to log old values")
                .HasColumnName("default_log_old_data");
            entity.Property(e => e.LogId)
                .HasComment("ID to trace a changing database schema")
                .HasColumnName("log_id");
            entity.Property(e => e.SchemaName)
                .HasComment("The name of the database schema")
                .HasColumnName("schema_name");
            entity.Property(e => e.TriggerCreateTable)
                .HasDefaultValue(false)
                .HasComment("Flag that shows if pgMemento starts auditing for newly created tables")
                .HasColumnName("trigger_create_table");
            entity.Property(e => e.TxidRange)
                .HasComment("Stores the transaction IDs when pgMemento has been activated or stopped in the schema")
                .HasColumnName("txid_range");
        });

        modelBuilder.Entity<AuditTable>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("audit_tables", "pgmemento");

            entity.Property(e => e.AuditIdColumn)
                .HasComment("Name of the audit_id column added to the audited table")
                .HasColumnName("audit_id_column");
            entity.Property(e => e.LogNewData)
                .HasComment("Flag that shows if new values are logged for audited table")
                .HasColumnName("log_new_data");
            entity.Property(e => e.LogOldData)
                .HasComment("Flag that shows if old values are logged for audited table")
                .HasColumnName("log_old_data");
            entity.Property(e => e.TgIsActive)
                .HasComment("Flag, that shows if logging is activated for the table or not")
                .HasColumnName("tg_is_active");
            entity.Property(e => e.TxidMax)
                .HasComment("The maximal transaction ID referenced to the audited table in the table_event_log")
                .HasColumnName("txid_max");
            entity.Property(e => e.TxidMin)
                .HasComment("The minimal transaction ID referenced to the audited table in the table_event_log")
                .HasColumnName("txid_min");
        });

        modelBuilder.Entity<AuditTableLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_table_log_pk");

            entity.ToTable("audit_table_log", "pgmemento", tb => tb.HasComment("Stores information about audited tables, which is important when restoring a whole schema or database"));

            entity.HasIndex(e => e.LogId, "table_log_idx");

            entity.HasIndex(e => new { e.TableName, e.SchemaName }, "table_log_name_idx");

            entity.HasIndex(e => e.TxidRange, "table_log_range_idx").HasMethod("gist");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('audit_table_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.AuditIdColumn)
                .HasComment("The name for the audit_id column added to the audited table")
                .HasColumnName("audit_id_column");
            entity.Property(e => e.LogId)
                .HasComment("ID to trace a changing table")
                .HasColumnName("log_id");
            entity.Property(e => e.LogNewData)
                .HasComment("Flag that shows if new values are logged for audited table")
                .HasColumnName("log_new_data");
            entity.Property(e => e.LogOldData)
                .HasComment("Flag that shows if old values are logged for audited table")
                .HasColumnName("log_old_data");
            entity.Property(e => e.Relid)
                .HasComment("[DEPRECATED] The table's OID to trace a table when changed")
                .HasColumnType("oid")
                .HasColumnName("relid");
            entity.Property(e => e.SchemaName)
                .HasComment("The schema the table belongs to")
                .HasColumnName("schema_name");
            entity.Property(e => e.TableName)
                .HasComment("The name of the table")
                .HasColumnName("table_name");
            entity.Property(e => e.TxidRange)
                .HasComment("Stores the transaction IDs when the table has been created and dropped")
                .HasColumnName("txid_range");
        });

        modelBuilder.Entity<AuditTablesDependency>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("audit_tables_dependency", "pgmemento");

            entity.Property(e => e.Depth)
                .HasComment("The depth of foreign key references")
                .HasColumnName("depth");
            entity.Property(e => e.Relid)
                .HasComment("The OID of the table")
                .HasColumnType("oid")
                .HasColumnName("relid");
            entity.Property(e => e.TableLogId)
                .HasComment("The tracing log ID from audit_table_log")
                .HasColumnName("table_log_id");
            entity.Property(e => e.Tablename)
                .HasComment("The name of the table")
                .HasColumnName("tablename");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable("categories");

            entity.HasIndex(e => e.PgmementoAuditId, "categories_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("comments_pkey");

            entity.ToTable("comments");

            entity.HasIndex(e => e.PgmementoAuditId, "comments_pgmemento_audit_id_key").IsUnique();

            entity.HasIndex(e => e.ParentCommentId, "idx_comments_parent_id");

            entity.HasIndex(e => e.VideoId, "idx_comments_video_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.ParentCommentId).HasColumnName("parent_comment_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.VideoId).HasColumnName("video_id");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("comments_parent_comment_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("comments_user_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.Comments)
                .HasForeignKey(d => d.VideoId)
                .HasConstraintName("comments_video_id_fkey");
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("images_pkey");

            entity.ToTable("images");

            entity.HasIndex(e => e.UserId, "idx_images_user_id");

            entity.HasIndex(e => e.PgmementoAuditId, "images_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Extension)
                .HasMaxLength(10)
                .HasColumnName("extension");
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.StatusId)
                .HasDefaultValue(1)
                .HasColumnName("status_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.Width).HasColumnName("width");

            entity.HasOne(d => d.Status).WithMany(p => p.Images)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("images_status_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Images)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("images_user_id_fkey");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("playlists_pkey");

            entity.ToTable("playlists");

            entity.HasIndex(e => e.UserId, "idx_playlists_user_id");

            entity.HasIndex(e => e.PgmementoAuditId, "playlists_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.VisibilityId)
                .HasDefaultValue(3)
                .HasColumnName("visibility_id");

            entity.HasOne(d => d.User).WithMany(p => p.Playlists)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("playlists_user_id_fkey");

            entity.HasOne(d => d.Visibility).WithMany(p => p.Playlists)
                .HasForeignKey(d => d.VisibilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("playlists_visibility_id_fkey");
        });

        modelBuilder.Entity<PlaylistVideo>(entity =>
        {
            entity.HasKey(e => new { e.PlaylistId, e.VideoId }).HasName("playlist_videos_pkey");

            entity.ToTable("playlist_videos");

            entity.HasIndex(e => e.VideoId, "idx_playlist_videos_video_id");

            entity.HasIndex(e => e.PgmementoAuditId, "playlist_videos_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.PlaylistId).HasColumnName("playlist_id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Playlist).WithMany(p => p.PlaylistVideos)
                .HasForeignKey(d => d.PlaylistId)
                .HasConstraintName("playlist_videos_playlist_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.PlaylistVideos)
                .HasForeignKey(d => d.VideoId)
                .HasConstraintName("playlist_videos_video_id_fkey");
        });

        modelBuilder.Entity<ProcessingStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("processing_statuses_pkey");

            entity.ToTable("processing_statuses");

            entity.HasIndex(e => e.PgmementoAuditId, "processing_statuses_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });

        modelBuilder.Entity<ReactionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("reaction_types_pkey");

            entity.ToTable("reaction_types");

            entity.HasIndex(e => e.PgmementoAuditId, "reaction_types_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });

        modelBuilder.Entity<RowLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("row_log_pk");

            entity.ToTable("row_log", "pgmemento", tb => tb.HasComment("Stores the historic data a.k.a the audit trail"));

            entity.HasIndex(e => e.AuditId, "row_log_audit_idx");

            entity.HasIndex(e => new { e.EventKey, e.AuditId }, "row_log_event_audit_idx").IsUnique();

            entity.HasIndex(e => e.NewData, "row_log_new_data_idx").HasMethod("gin");

            entity.HasIndex(e => e.OldData, "row_log_old_data_idx").HasMethod("gin");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('row_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.AuditId)
                .HasComment(" The implicit link to a table's row")
                .HasColumnName("audit_id");
            entity.Property(e => e.EventKey)
                .HasComment("Concatenated information of table event")
                .HasColumnName("event_key");
            entity.Property(e => e.NewData)
                .HasComment("The new values of changed columns in a JSONB object")
                .HasColumnType("jsonb")
                .HasColumnName("new_data");
            entity.Property(e => e.OldData)
                .HasComment("The old values of changed columns in a JSONB object")
                .HasColumnType("jsonb")
                .HasColumnName("old_data");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => new { e.SubscriberId, e.ChannelId }).HasName("subscriptions_pkey");

            entity.ToTable("subscriptions");

            entity.HasIndex(e => e.ChannelId, "idx_subscriptions_channel_id");

            entity.HasIndex(e => e.PgmementoAuditId, "subscriptions_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.SubscriberId).HasColumnName("subscriber_id");
            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Channel).WithMany(p => p.SubscriptionChannels)
                .HasForeignKey(d => d.ChannelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subscriptions_channel_id_fkey");

            entity.HasOne(d => d.Subscriber).WithMany(p => p.SubscriptionSubscribers)
                .HasForeignKey(d => d.SubscriberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("subscriptions_subscriber_id_fkey");
        });

        modelBuilder.Entity<TableEventLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("table_event_log_pk");

            entity.ToTable("table_event_log", "pgmemento", tb => tb.HasComment("Stores metadata about different kind of events happening during one transaction against one table"));

            entity.HasIndex(e => e.EventKey, "table_event_log_event_idx").IsUnique();

            entity.HasIndex(e => e.TransactionId, "table_event_log_fk_idx");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('table_event_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.EventKey)
                .HasComment("Concatenated information of most columns")
                .HasColumnName("event_key");
            entity.Property(e => e.OpId)
                .HasComment("ID of event type")
                .HasColumnName("op_id");
            entity.Property(e => e.SchemaName)
                .HasComment("Schema of firing table")
                .HasColumnName("schema_name");
            entity.Property(e => e.StmtTime)
                .HasComment("Stores the result of statement_timestamp() function")
                .HasColumnName("stmt_time");
            entity.Property(e => e.TableName)
                .HasComment("Name of table that fired the trigger")
                .HasColumnName("table_name");
            entity.Property(e => e.TableOperation)
                .HasComment("Text for of event type")
                .HasColumnName("table_operation");
            entity.Property(e => e.TransactionId)
                .HasComment("Foreign Key to transaction_log table")
                .HasColumnName("transaction_id");

            entity.HasOne(d => d.Transaction).WithMany(p => p.TableEventLogs)
                .HasForeignKey(d => d.TransactionId)
                .HasConstraintName("table_event_log_txid_fk");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.ToTable("tags");

            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.HasIndex(e => e.PgmementoAuditId, "tags_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });

        modelBuilder.Entity<TransactionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transaction_log_pk");

            entity.ToTable("transaction_log", "pgmemento", tb => tb.HasComment("Stores metadata about each transaction"));

            entity.HasIndex(e => e.SessionInfo, "transaction_log_session_idx").HasMethod("gin");

            entity.HasIndex(e => new { e.TxidTime, e.Txid }, "transaction_log_unique_idx").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('transaction_log_id_seq'::regclass)")
                .HasComment("The Primary Key")
                .HasColumnName("id");
            entity.Property(e => e.ApplicationName)
                .HasComment("Stores the output of current_setting('application_name')")
                .HasColumnName("application_name");
            entity.Property(e => e.ClientName)
                .HasComment("Stores the result of inet_client_addr() function")
                .HasColumnName("client_name");
            entity.Property(e => e.ClientPort)
                .HasComment("Stores the result of inet_client_port() function")
                .HasColumnName("client_port");
            entity.Property(e => e.ProcessId)
                .HasComment("Stores the result of pg_backend_pid() function")
                .HasColumnName("process_id");
            entity.Property(e => e.SessionInfo)
                .HasComment("Stores any infos a client/user defines beforehand with set_config")
                .HasColumnType("jsonb")
                .HasColumnName("session_info");
            entity.Property(e => e.Txid)
                .HasComment("The internal transaction ID by PostgreSQL (can cycle)")
                .HasColumnName("txid");
            entity.Property(e => e.TxidTime)
                .HasComment("Stores the result of transaction_timestamp() function")
                .HasColumnName("txid_time");
            entity.Property(e => e.UserName)
                .HasComment("Stores the result of session_user function")
                .HasColumnName("user_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.PgmementoAuditId, "users_pgmemento_audit_id_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.AvatarImageId).HasColumnName("avatar_image_id");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.AvatarImage).WithMany(p => p.Users)
                .HasForeignKey(d => d.AvatarImageId)
                .HasConstraintName("fk_users_avatar_image");
        });

        modelBuilder.Entity<Video>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("videos_pkey");

            entity.ToTable("videos");

            entity.HasIndex(e => new { e.VisibilityId, e.PublishedAt }, "idx_videos_listing")
                .IsDescending(false, true)
                .HasFilter("(active = true)");

            entity.HasIndex(e => e.SearchVector, "idx_videos_search").HasMethod("gin");

            entity.HasIndex(e => e.StatusId, "idx_videos_status_id");

            entity.HasIndex(e => e.UserId, "idx_videos_user_id");

            entity.HasIndex(e => e.PgmementoAuditId, "videos_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CommentCount).HasColumnName("comment_count");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DislikeCount).HasColumnName("dislike_count");
            entity.Property(e => e.FileSizeBytes)
                .HasDefaultValue(0L)
                .HasColumnName("file_size_bytes");
            entity.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
            entity.Property(e => e.LikeCount).HasColumnName("like_count");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.SearchVector)
                .HasComputedColumnSql("(setweight(to_tsvector('simple'::regconfig, (COALESCE(title, ''::character varying))::text), 'A'::\"char\") || setweight(to_tsvector('simple'::regconfig, COALESCE(description, ''::text)), 'B'::\"char\"))", true)
                .HasColumnName("search_vector");
            entity.Property(e => e.StatusId)
                .HasDefaultValue(1)
                .HasColumnName("status_id");
            entity.Property(e => e.ThumbnailImageId).HasColumnName("thumbnail_image_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.ViewCount).HasColumnName("view_count");
            entity.Property(e => e.VisibilityId)
                .HasDefaultValue(3)
                .HasColumnName("visibility_id");

            entity.HasOne(d => d.Category).WithMany(p => p.Videos)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("videos_category_id_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Videos)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("videos_status_id_fkey");

            entity.HasOne(d => d.ThumbnailImage).WithMany(p => p.Videos)
                .HasForeignKey(d => d.ThumbnailImageId)
                .HasConstraintName("videos_thumbnail_image_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Videos)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("videos_user_id_fkey");

            entity.HasOne(d => d.Visibility).WithMany(p => p.Videos)
                .HasForeignKey(d => d.VisibilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("videos_visibility_id_fkey");
        });

        modelBuilder.Entity<VideoReaction>(entity =>
        {
            entity.HasKey(e => new { e.VideoId, e.UserId }).HasName("video_reactions_pkey");

            entity.ToTable("video_reactions");

            entity.HasIndex(e => e.PgmementoAuditId, "video_reactions_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.ReactionTypeId).HasColumnName("reaction_type_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.ReactionType).WithMany(p => p.VideoReactions)
                .HasForeignKey(d => d.ReactionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("video_reactions_reaction_type_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.VideoReactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("video_reactions_user_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.VideoReactions)
                .HasForeignKey(d => d.VideoId)
                .HasConstraintName("video_reactions_video_id_fkey");
        });

        modelBuilder.Entity<SavedVideo>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.VideoId }).HasName("saved_videos_pkey");

            entity.ToTable("saved_videos");

            entity.HasIndex(e => e.VideoId, "idx_saved_videos_video_id");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.User).WithMany(p => p.SavedVideos)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("saved_videos_user_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.SavedVideos)
                .HasForeignKey(d => d.VideoId)
                .HasConstraintName("saved_videos_video_id_fkey");
        });

        modelBuilder.Entity<VideoTag>(entity =>
        {
            entity.HasKey(e => new { e.VideoId, e.TagId }).HasName("video_tags_pkey");

            entity.ToTable("video_tags");

            entity.HasIndex(e => e.TagId, "idx_video_tags_tag_id");

            entity.HasIndex(e => e.PgmementoAuditId, "video_tags_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Tag).WithMany(p => p.VideoTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("video_tags_tag_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.VideoTags)
                .HasForeignKey(d => d.VideoId)
                .HasConstraintName("video_tags_video_id_fkey");
        });

        modelBuilder.Entity<VideoView>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("video_views_pkey");

            entity.ToTable("video_views");

            entity.HasIndex(e => e.VideoId, "idx_video_views_video_id");

            entity.HasIndex(e => e.WatchedAt, "idx_video_views_watched_at");

            entity.HasIndex(e => e.PgmementoAuditId, "video_views_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.WatchedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("watched_at");
            entity.Property(e => e.WatchedSeconds).HasColumnName("watched_seconds");

            entity.HasOne(d => d.User).WithMany(p => p.VideoViews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("video_views_user_id_fkey");

            entity.HasOne(d => d.Video).WithMany(p => p.VideoViews)
                .HasForeignKey(d => d.VideoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("video_views_video_id_fkey");
        });

        modelBuilder.Entity<Visibility>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("visibilities_pkey");

            entity.ToTable("visibilities");

            entity.HasIndex(e => e.PgmementoAuditId, "visibilities_pgmemento_audit_id_key").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("create_date");
            entity.Property(e => e.CreateUserId).HasColumnName("create_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ModifyDate).HasColumnName("modify_date");
            entity.Property(e => e.ModifyUserId).HasColumnName("modify_user_id");
            entity.Property(e => e.PgmementoAuditId)
                .HasDefaultValueSql("nextval('audit_id_seq'::regclass)")
                .HasColumnName("pgmemento_audit_id");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });
        modelBuilder.Entity<RelatedVideoResult>(entity =>
        {
            entity.HasNoKey();
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Score).HasColumnName("score");
        });

        modelBuilder.HasDbFunction(typeof(AsyncApiDbContext)
                .GetMethod(nameof(GetRelatedVideos), [typeof(Guid), typeof(Guid?), typeof(int)])!)
            .HasName("get_related_videos")
            .HasSchema("public");


        modelBuilder.HasSequence("audit_id_seq", "pgmemento")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("schema_log_id_seq", "pgmemento")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("table_log_id_seq", "pgmemento")
            .HasMin(0L)
            .HasMax(2147483647L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
