using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Social;

/// <summary>
/// Stories, media, likes, comments, follows and reviews. Writes to a user's own rows are filtered by
/// that user's id in SQL (author, owner, follower), never trusted from the caller alone.
/// </summary>
public sealed class SocialRepository(IDbConnectionFactory connectionFactory) : ISocialRepository
{
    public async Task<long> AddMediaAsync(long ownerId, MediaKind kind, string contentType, string uploadBlob, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Social].[Media] ([OwnerId], [Kind], [ContentType], [UploadBlob], [UpdatedId])
            OUTPUT inserted.[Id]
            VALUES (@OwnerId, @Kind, @ContentType, @UploadBlob, @OwnerId);
            """,
            new
            {
                OwnerId = ownerId,
                Kind = (byte)kind,
                ContentType = new DbString { Value = contentType, IsAnsi = true, Length = 100 },
                UploadBlob = new DbString { Value = uploadBlob, IsAnsi = true, Length = 200 },
            },
            cancellationToken: cancellationToken));
    }

    public async Task<MediaRecord?> GetMediaAsync(long mediaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MediaRow>(new CommandDefinition(
            """
            SELECT [Id], [OwnerId], [Kind], [ContentType], [UploadBlob], [ProcessedBlob], [Status]
            FROM   [Social].[Media]
            WHERE  [Id] = @Id AND [Archived] = 0;
            """,
            new { Id = mediaId },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new MediaRecord(row.Id, row.OwnerId, (MediaKind)row.Kind, row.ContentType, row.UploadBlob, row.ProcessedBlob, (MediaStatus)row.Status);
    }

    public async Task<bool> SetMediaProcessingAsync(long mediaId, long ownerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[Media]
            SET    [Status] = 2, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @OwnerId
            WHERE  [Id] = @Id AND [OwnerId] = @OwnerId AND [Status] = 1 AND [Archived] = 0;
            """,
            new { Id = mediaId, OwnerId = ownerId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task SetMediaReadyAsync(long mediaId, string processedBlob, long sizeBytes, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[Media]
            SET    [Status] = 3, [ProcessedBlob] = @ProcessedBlob, [SizeBytes] = @SizeBytes, [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [Id] = @Id AND [Status] = 2;
            """,
            new { Id = mediaId, ProcessedBlob = new DbString { Value = processedBlob, IsAnsi = true, Length = 200 }, SizeBytes = sizeBytes },
            cancellationToken: cancellationToken));
    }

    public async Task SetMediaFailedAsync(long mediaId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[Media]
            SET    [Status] = 4, [Failure] = @Reason, [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [Id] = @Id AND [Status] IN (1, 2);
            """,
            new { Id = mediaId, Reason = reason.Length > 200 ? reason[..200] : reason },
            cancellationToken: cancellationToken));
    }

    public async Task<long?> AddPostAsync(NewPost post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);

        using var media = new DataTable();
        media.Columns.Add("Id", typeof(long));
        foreach (var id in post.MediaIds)
        {
            media.Rows.Add(id);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@AuthorId", post.AuthorId, DbType.Int64);
        parameters.Add("@Body", post.Body, DbType.String, size: 2000);
        parameters.Add("@DestinationId", post.DestinationId, DbType.Int64);
        parameters.Add("@TripId", post.TripId, DbType.Int64);
        parameters.Add("@MediaIds", media.AsTableValuedParameter("[Main].[IdList]"));
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Social.AddPost, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") == 0 ? parameters.Get<long>("@Id") : null;
    }

    public async Task<bool> ArchivePostAsync(long postId, long authorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[Post]
            SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @AuthorId
            WHERE  [Id] = @Id AND [AuthorId] = @AuthorId AND [Archived] = 0;
            """,
            new { Id = postId, AuthorId = authorId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<PostPage> QueryPostsAsync(long? viewerId, long? authorId, long? beforeId, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Social.QueryPosts,
            new { ViewerId = viewerId, AuthorId = authorId, BeforeId = beforeId, Take = Math.Clamp(take, 1, 50) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var posts = (await results.ReadAsync<PostRow>()).ToList();
        var media = (await results.ReadAsync<PostMediaRow>()).ToLookup(row => row.PostId);

        var items = posts.Select(row => new PostView(
            row.Id,
            row.AuthorId,
            row.AuthorName,
            row.AuthorVerifiedLevel is null ? null : (VerificationLevel)row.AuthorVerifiedLevel.Value,
            row.Body,
            row.DestinationSlug,
            row.DestinationName,
            row.DestinationNameBn,
            row.TripId,
            row.Likes,
            row.Comments,
            row.LikedByMe,
            AsUtc(row.Created),
            // The blob name; MediaLinks turns it into a short-lived read link.
            [.. media[row.Id].Select(item => new MediaView(item.Id, (MediaKind)item.Kind, item.ContentType, item.ProcessedBlob ?? string.Empty))]))
            .ToList();

        return new PostPage(items, items.Count == take ? items[^1].Id : null);
    }

    public async Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Social].[Post] WHERE [Id] = @Id AND [Archived] = 0 AND [Status] = 1;",
            new { Id = postId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task SetLikedAsync(long postId, long userId, bool liked, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            liked
                ? """
                  INSERT INTO [Social].[Like] ([PostId], [UserId], [UpdatedId])
                  SELECT @PostId, @UserId, @UserId
                  WHERE  NOT EXISTS (SELECT 1 FROM [Social].[Like] WITH (UPDLOCK, HOLDLOCK)
                                     WHERE [PostId] = @PostId AND [UserId] = @UserId AND [Archived] = 0);
                  """
                : """
                  UPDATE [Social].[Like]
                  SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @UserId
                  WHERE  [PostId] = @PostId AND [UserId] = @UserId AND [Archived] = 0;
                  """,
            new { PostId = postId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<CommentView> AddCommentAsync(long postId, long authorId, string body, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleAsync<CommentRow>(new CommandDefinition(
            """
            DECLARE @Inserted TABLE ([Id] BIGINT NOT NULL);

            INSERT INTO [Social].[Comment] ([PostId], [AuthorId], [Body], [UpdatedId])
            OUTPUT inserted.[Id] INTO @Inserted ([Id])
            VALUES (@PostId, @AuthorId, @Body, @AuthorId);

            SELECT [c].[Id], [c].[PostId], [c].[AuthorId], [u].[DisplayName] AS [AuthorName], [c].[Body], [c].[Created]
            FROM   [Social].[Comment] AS [c]
            JOIN   @Inserted          AS [i] ON [i].[Id] = [c].[Id]
            JOIN   [Main].[User]      AS [u] ON [u].[Id] = [c].[AuthorId];
            """,
            new { PostId = postId, AuthorId = authorId, Body = body },
            cancellationToken: cancellationToken));

        return ToComment(row);
    }

    public async Task<IReadOnlyList<CommentView>> QueryCommentsAsync(long postId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CommentRow>(new CommandDefinition(
            """
            SELECT   TOP (200) [c].[Id], [c].[PostId], [c].[AuthorId], [u].[DisplayName] AS [AuthorName], [c].[Body], [c].[Created]
            FROM     [Social].[Comment] AS [c]
            JOIN     [Main].[User]      AS [u] ON [u].[Id] = [c].[AuthorId]
            WHERE    [c].[PostId] = @PostId AND [c].[Archived] = 0
            ORDER BY [c].[Id];
            """,
            new { PostId = postId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(ToComment)];
    }

    public async Task<bool> ArchiveCommentAsync(long commentId, long authorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[Comment]
            SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @AuthorId
            WHERE  [Id] = @Id AND [AuthorId] = @AuthorId AND [Archived] = 0;
            """,
            new { Id = commentId, AuthorId = authorId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task SetFollowingAsync(long followerId, long followeeId, bool following, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            following
                ? """
                  INSERT INTO [Social].[Follow] ([FollowerId], [FolloweeId], [UpdatedId])
                  SELECT @FollowerId, @FolloweeId, @FollowerId
                  WHERE  NOT EXISTS (SELECT 1 FROM [Social].[Follow] WITH (UPDLOCK, HOLDLOCK)
                                     WHERE [FollowerId] = @FollowerId AND [FolloweeId] = @FolloweeId AND [Archived] = 0);
                  """
                : """
                  UPDATE [Social].[Follow]
                  SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @FollowerId
                  WHERE  [FollowerId] = @FollowerId AND [FolloweeId] = @FolloweeId AND [Archived] = 0;
                  """,
            new { FollowerId = followerId, FolloweeId = followeeId },
            cancellationToken: cancellationToken));
    }

    public async Task<PublicProfile?> GetPublicProfileAsync(long userId, long? viewerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Social.GetPublicProfile,
            new { UserId = userId, ViewerId = viewerId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var row = await results.ReadSingleOrDefaultAsync<ProfileRow>();
        var trips = (await results.ReadAsync<TripRow>()).ToList();
        var reviews = (await results.ReadAsync<ReviewRow>()).ToList();

        if (row is null)
        {
            return null;
        }

        return new PublicProfile(
            row.UserId,
            row.DisplayName,
            row.Bio,
            row.HomeDistrict,
            DateOnly.FromDateTime(row.Created),
            row.VerifiedLevel is null ? null : (VerificationLevel)row.VerifiedLevel.Value,
            row.IsHost,
            row.Followers,
            row.Following,
            row.FollowedByMe,
            row.AsHostCount,
            row.AsHostAverage,
            row.AsTravelerCount,
            row.AsTravelerAverage,
            [.. trips.Select(trip => new ProfileTrip(
                trip.Id, trip.Title, trip.DestinationSlug, trip.DestinationName, trip.DestinationNameBn,
                DateOnly.FromDateTime(trip.StartDate), DateOnly.FromDateTime(trip.EndDate), (TripStatus)trip.Status))],
            [.. reviews.Select(review => new ReviewView(
                review.Id, review.TripId, review.TripTitle, review.ReviewerId, review.ReviewerName,
                (ReviewDirection)review.Direction, review.Rating, review.Body, AsUtc(review.Created)))]);
    }

    public async Task<(ReviewOutcome Outcome, long? ReviewId)> AddReviewAsync(NewReview review, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@TripId", review.TripId, DbType.Int64);
        parameters.Add("@ReviewerId", review.ReviewerId, DbType.Int64);
        parameters.Add("@RevieweeId", review.RevieweeId, DbType.Int64);
        parameters.Add("@Direction", (byte)review.Direction, DbType.Byte);
        parameters.Add("@Rating", review.Rating, DbType.Byte);
        parameters.Add("@Body", review.Body, DbType.String, size: 1000);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Social.AddReview, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return ((ReviewOutcome)parameters.Get<byte>("@Result"), parameters.Get<long?>("@Id"));
    }

    public async Task<IReadOnlyList<Reviewable>> QueryReviewableAsync(long tripId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ReviewableRow>(new CommandDefinition(
            Procedures.Social.QueryReviewable,
            new { TripId = tripId, UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new Reviewable(row.UserId, row.DisplayName, (ReviewDirection)row.Direction, row.AlreadyReviewed))];
    }

    private static CommentView ToComment(CommentRow row) =>
        new(row.Id, row.PostId, row.AuthorId, row.AuthorName, row.Body, AsUtc(row.Created));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record MediaRow(long Id, long OwnerId, byte Kind, string ContentType, string UploadBlob, string? ProcessedBlob, byte Status);

    private sealed record PostRow(
        long Id,
        long AuthorId,
        string? AuthorName,
        byte? AuthorVerifiedLevel,
        string Body,
        string? DestinationSlug,
        string? DestinationName,
        string? DestinationNameBn,
        long? TripId,
        int Likes,
        int Comments,
        bool LikedByMe,
        DateTime Created);

    private sealed record PostMediaRow(long Id, long PostId, byte Kind, string ContentType, string? ProcessedBlob);

    private sealed record CommentRow(long Id, long PostId, long AuthorId, string? AuthorName, string Body, DateTime Created);

    private sealed record ProfileRow(
        long UserId,
        string? DisplayName,
        string? Bio,
        string? HomeDistrict,
        DateTime Created,
        byte? VerifiedLevel,
        bool IsHost,
        int Followers,
        int Following,
        bool FollowedByMe,
        int AsHostCount,
        decimal? AsHostAverage,
        int AsTravelerCount,
        decimal? AsTravelerAverage);

    private sealed record TripRow(
        long Id,
        string Title,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        DateTime StartDate,
        DateTime EndDate,
        byte Status);

    private sealed record ReviewRow(
        long Id,
        long TripId,
        string TripTitle,
        long ReviewerId,
        string? ReviewerName,
        byte Direction,
        byte Rating,
        string? Body,
        DateTime Created);

    private sealed record ReviewableRow(long UserId, string? DisplayName, byte Direction, bool AlreadyReviewed);
}
