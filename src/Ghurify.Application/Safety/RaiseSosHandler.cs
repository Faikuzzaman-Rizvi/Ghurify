using System.Globalization;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// Someone on a trip needs help. In this order, and each step on its own so one failing never stops
/// the next: record it, put it on the safety desk's live board, text their emergency contact, tell
/// the host, and hand back the nearest police and hospitals.
/// </summary>
public sealed class RaiseSosHandler(
    ISafetyRepository safety,
    ISafetyBroadcaster broadcaster,
    ISmsSender sms,
    NotificationService notifications,
    ILogger<RaiseSosHandler> logger)
{
    public async Task<Result<SosRaisedView>> HandleAsync(long userId, long tripId, RaiseSosCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Latitude is < -90 or > 90 || command.Longitude is < -180 or > 180)
        {
            return AppError.Validation("sos_position", "The position is not valid.");
        }

        var message = string.IsNullOrWhiteSpace(command.Message) ? null : command.Message.Trim();
        if (message is { Length: > 500 })
        {
            message = message[..500];
        }

        var raised = await safety.AddSosAsync(
            new NewSos(userId, tripId, command.Latitude, command.Longitude, command.AccuracyMeters, message), cancellationToken);

        if (raised is null)
        {
            return AppError.NotFound("trip_not_found", "You are not on this trip.");
        }

        var sos = raised.Sos;
        logger.LogWarning("SOS {SosId} raised by user {UserId} on trip {TripId}.", sos.Id, userId, tripId);

        await TryAsync(() => broadcaster.SosAsync(sos, cancellationToken), "push to the safety desk", sos.Id);

        var contactTexted = false;
        if (PhoneNumber.TryParse(raised.EmergencyContactPhone, out var contact))
        {
            contactTexted = await TrySendAsync(contact.Value, SosText(sos), sos.Id, cancellationToken);
        }

        if (raised.HostId != userId)
        {
            await TryAsync(
                () => notifications.NotifyAsync(
                    raised.HostId,
                    NotificationKinds.SosRaised,
                    $"safety.sos:{sos.Id}",
                    new { tripId, sosId = sos.Id, name = sos.UserName },
                    cancellationToken),
                "tell the host",
                sos.Id);
        }

        return new SosRaisedView(sos.Id, contactTexted, raised.NearestHelp);
    }

    /// <summary>
    /// The SMS to the emergency contact, in Bangla and English: they may read either, and there is
    /// no profile to tell which. A map link, never the person's own phone number.
    /// </summary>
    private static string SosText(SosBoardItem sos)
    {
        var map = string.Create(CultureInfo.InvariantCulture, $"https://maps.google.com/?q={sos.Latitude},{sos.Longitude}");
        var name = sos.UserName ?? "A GhuriFiri traveller";
        return $"GhuriFiri SOS: {name} needs help on \"{sos.TripTitle}\". {map} Call 999. "
            + $"ঘুরিফিরি SOS: {name} বিপদে আছেন। অবস্থান: {map} জরুরি সাহায্যের জন্য ৯৯৯-এ ফোন করুন।";
    }

    private async Task<bool> TrySendAsync(PhoneNumber to, string text, long sosId, CancellationToken cancellationToken)
    {
        try
        {
            var sent = await sms.SendAsync(to, text, cancellationToken);
            if (!sent)
            {
                logger.LogWarning("SOS {SosId}: the emergency contact could not be texted (no SMS provider).", sosId);
            }

            return sent;
        }
#pragma warning disable CA1031 // An SOS must reach everyone it can; one channel failing must not stop the rest.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "SOS {SosId}: texting the emergency contact failed.", sosId);
            return false;
        }
    }

    private async Task TryAsync(Func<Task> action, string what, long sosId)
    {
        try
        {
            await action();
        }
#pragma warning disable CA1031 // As above: keep going.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "SOS {SosId}: could not {What}.", sosId, what);
        }
    }
}

public sealed record RaiseSosCommand(decimal Latitude, decimal Longitude, int? AccuracyMeters, string? Message);

/// <summary>What the person in trouble sees: that help is on its way, and where the nearest help is.</summary>
public sealed record SosRaisedView(long SosId, bool EmergencyContactTexted, IReadOnlyList<HelpPoint> NearestHelp);
