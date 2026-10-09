using FluentValidation;
using Ghurify.Application.Admin;
using Ghurify.Application.Bookings;
using Ghurify.Application.Chat;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Application.Payments;
using Ghurify.Application.Safety;
using Ghurify.Application.Site;
using Ghurify.Application.Social;
using Ghurify.Application.Trips;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.Application;

/// <summary>
/// Registers the use cases and their validators. Adapters are wired separately, in
/// Ghurify.Infrastructure, so this layer stays unaware of how anything is implemented.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddGhurifyApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // A missing or too-short signing key or pepper stops the host from starting.
        services
            .AddOptions<IdentityOptions>()
            .Bind(configuration.GetSection(IdentityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<EmailCodes>();
        services.AddScoped<SessionStarter>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<ResendSignUpCodeHandler>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();

        services.AddScoped<IValidator<RegisterCommand>, RegisterCommandValidator>();
        services.AddScoped<IValidator<ConfirmEmailCommand>, ConfirmEmailCommandValidator>();
        services.AddScoped<IValidator<EmailOnlyCommand>, EmailOnlyCommandValidator>();
        services.AddScoped<IValidator<SignInCommand>, SignInCommandValidator>();
        services.AddScoped<IValidator<ResetPasswordCommand>, ResetPasswordCommandValidator>();
        services.AddScoped<IValidator<ChangePasswordCommand>, ChangePasswordCommandValidator>();

        // --- Profiles, roles and verification ---
        services
            .AddOptions<VerificationOptions>()
            .Bind(configuration.GetSection(VerificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<AccessService>();
        services.AddScoped<GetProfileHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<BecomeHostHandler>();
        services.AddScoped<ChangeRoleHandler>();
        services.AddScoped<StartVerificationHandler>();
        services.AddScoped<StartDocumentUploadHandler>();
        services.AddScoped<CompleteDocumentUploadHandler>();
        services.AddScoped<RemoveDocumentHandler>();
        services.AddScoped<ListMyDocumentsHandler>();
        services.AddScoped<GetVerificationDocumentsHandler>();
        services.AddScoped<PurgeVerificationDocumentsHandler>();
        services.AddScoped<StartAvatarUploadHandler>();
        services.AddScoped<CompleteAvatarUploadHandler>();
        services.AddScoped<RemoveAvatarHandler>();
        services.AddScoped<GetAvatarLinkHandler>();
        services.AddScoped<GetMyVerificationsHandler>();
        services.AddScoped<HandleVerificationCallbackHandler>();
        services.AddScoped<QueryVerificationQueueHandler>();
        services.AddScoped<ReviewVerificationHandler>();

        services.AddScoped<IValidator<UpdateProfileCommand>, UpdateProfileCommandValidator>();

        // --- Trips ---
        services.AddScoped<TripViewer>();
        services.AddScoped<SearchTripsHandler>();
        services.AddScoped<GetTripHandler>();
        services.AddScoped<ListDestinationsHandler>();
        services.AddScoped<CreateTripHandler>();
        services.AddScoped<UpdateTripHandler>();
        services.AddScoped<PublishTripHandler>();
        services.AddScoped<ListHostTripsHandler>();

        services.AddScoped<IValidator<SearchTripsQuery>, SearchTripsQueryValidator>();
        services.AddScoped<IValidator<SaveTripCommand>, SaveTripCommandValidator>();

        // --- Bookings and notifications ---
        services
            .AddOptions<BookingOptions>()
            .Bind(configuration.GetSection(BookingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<NotificationService>();
        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<MarkNotificationsReadHandler>();
        services.AddScoped<RequestToJoinHandler>();
        services.AddScoped<ApproveJoinRequestHandler>();
        services.AddScoped<DeclineJoinRequestHandler>();
        services.AddScoped<CancelJoinRequestHandler>();
        services.AddScoped<ListTripJoinRequestsHandler>();
        services.AddScoped<ListMyBookingsHandler>();
        services.AddScoped<ReleaseExpiredHoldsHandler>();

        // --- Payments ---
        services
            .AddOptions<PaymentsOptions>()
            .Bind(configuration.GetSection(PaymentsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.Equals(options.Provider, PaymentsOptions.SslCommerzProvider, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(options.SslCommerz.StoreId) && !string.IsNullOrWhiteSpace(options.SslCommerz.StorePassword)),
                "Payments:SslCommerz:StoreId and StorePassword are required when Payments:Provider is sslcommerz.")
            .Validate(
                options => options.Provider is PaymentsOptions.FakeProvider or PaymentsOptions.SslCommerzProvider,
                "Payments:Provider must be \"fake\" or \"sslcommerz\".")
            .ValidateOnStart();

        services.AddScoped<RefundService>();
        services.AddScoped<GetCheckoutHandler>();
        services.AddScoped<StartPaymentHandler>();
        services.AddScoped<HandlePaymentCallbackHandler>();
        services.AddScoped<RetryRefundsHandler>();
        services.AddScoped<ListMyRefundsHandler>();
        services.AddScoped<ListMyPaymentsHandler>();
        services.AddScoped<GetMyPaymentHandler>();
        services.AddScoped<ListReceivedPaymentsHandler>();

        // --- Chat, payouts, cancellations ---
        services
            .AddOptions<PayoutOptions>()
            .Bind(configuration.GetSection(PayoutOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ChatMembership>();
        services.AddScoped<GetChatHistoryHandler>();
        services.AddScoped<SendChatMessageHandler>();
        services.AddScoped<MarkChatReadHandler>();
        services.AddScoped<ListChatUnreadHandler>();
        services.AddScoped<TripCancellationService>();
        services.AddScoped<CancelTripHandler>();
        services.AddScoped<CompleteFinishedTripsHandler>();
        services.AddScoped<GetMyTravelMapHandler>();
        services.AddScoped<GetSharedTravelMapHandler>();
        services.AddScoped<AddVisitHandler>();
        services.AddScoped<EditVisitHandler>();
        services.AddScoped<RemoveVisitHandler>();
        services.AddScoped<AddVisitPhotosHandler>();
        services.AddScoped<RemoveVisitPhotoHandler>();
        services.AddScoped<SetTravelMapSharingHandler>();
        services.AddScoped<GetCancellationQuoteHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<ReleaseDuePayoutsHandler>();
        services.AddScoped<ListMyPayoutsHandler>();
        services.AddScoped<ListPayoutQueueHandler>();
        services.AddScoped<ApprovePayoutHandler>();

        // --- Social ---
        services
            .AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<MediaLinks>();
        services.AddScoped<CreateUploadUrlHandler>();
        services.AddScoped<CompleteUploadHandler>();
        services.AddScoped<ProcessMediaHandler>();
        services.AddScoped<CreatePostHandler>();
        services.AddScoped<DeletePostHandler>();
        services.AddScoped<EditPostHandler>();
        services.AddScoped<RemovePostHandler>();
        services.AddScoped<ListAllPostsHandler>();
        services.AddScoped<GetFeedHandler>();
        services.AddScoped<LikePostHandler>();
        services.AddScoped<AddCommentHandler>();
        services.AddScoped<ListCommentsHandler>();
        services.AddScoped<DeleteCommentHandler>();
        services.AddScoped<FollowUserHandler>();
        services.AddScoped<GetPublicProfileHandler>();
        services.AddScoped<AddReviewHandler>();
        services.AddScoped<ListReviewableHandler>();

        // --- Admin portal ---
        services.AddScoped<SearchUsersHandler>();
        services.AddScoped<GetUserDetailHandler>();
        services.AddScoped<SetUserStatusHandler>();
        services.AddScoped<RequirePasswordResetHandler>();
        services.AddScoped<SearchAllTripsHandler>();
        services.AddScoped<CancelTripAsAdminHandler>();
        services.AddScoped<GetBookingForAdminHandler>();
        services.AddScoped<SearchPaymentsHandler>();
        services.AddScoped<GetPaymentForAdminHandler>();
        services.AddScoped<RetryRefundsNowHandler>();
        services.AddScoped<SaveDestinationHandler>();
        services.AddScoped<SaveEmergencyPointHandler>();
        services.AddScoped<ListEmergencyPointsHandler>();

        // --- Super admin: roles, permissions, step-up and the audit trail ---
        services.AddScoped<ListStaffRolesHandler>();
        services.AddScoped<SaveStaffRoleHandler>();
        services.AddScoped<DeleteStaffRoleHandler>();
        services.AddScoped<ListStaffMembersHandler>();
        services.AddScoped<AssignStaffRoleHandler>();
        services.AddScoped<StartStepUpHandler>();
        services.AddScoped<QueryAuditLogHandler>();

        services.AddScoped<IValidator<SaveStaffRoleCommand>, SaveStaffRoleCommandValidator>();

        // --- The site's own settings: branding and theme ---
        // The configuration is read on every page load, so it is held in an IMemoryCache (see
        // SiteConfigService). The cache itself is registered in Ghurify.Infrastructure.
        services.AddScoped<SiteConfigService>();
        services.AddScoped<GetSiteConfigHandler>();
        services.AddScoped<GetSettingsHandler>();
        services.AddScoped<SaveSettingsHandler>();
        services.AddScoped<GetSettingHistoryHandler>();
        services.AddScoped<UploadSiteAssetHandler>();
        services.AddScoped<RemoveSiteAssetHandler>();
        services.AddScoped<GetSiteAssetHandler>();

        // --- Safety and the admin desk ---
        services.AddScoped<RaiseSosHandler>();
        services.AddScoped<UpdateSosLocationHandler>();
        services.AddScoped<SetSosStatusHandler>();
        services.AddScoped<GetSosBoardHandler>();
        services.AddScoped<ScheduleCheckInHandler>();
        services.AddScoped<ListCheckInsHandler>();
        services.AddScoped<CompleteCheckInHandler>();
        services.AddScoped<FlagMissedCheckInsHandler>();
        services.AddScoped<ListMissedCheckInsHandler>();
        services.AddScoped<ChangeDestinationStatusHandler>();
        services.AddScoped<CloseDestinationHandler>();
        services.AddScoped<FileReportHandler>();
        services.AddScoped<ListReportsHandler>();
        services.AddScoped<ResolveReportHandler>();
        services.AddScoped<GetDashboardHandler>();

        return services;
    }
}
