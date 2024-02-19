using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless;

public static class EventExtensions
{
    public static bool HasError(this Event ev)
    {
        return ev.Data is not null && ev.Data.ContainsKey(Event.KnownDataKeys.Error);
    }

    public static Error? GetError(this Event ev)
    {
        if (!ev.HasError())
            return null;

        try
        {
            return ev.Data!.GetValue<Error>(Event.KnownDataKeys.Error);
        }
        catch (Exception) { }

        return null;
    }
    public static bool HasSimpleError(this Event ev)
    {
        return ev.Data is not null && ev.Data.ContainsKey(Event.KnownDataKeys.SimpleError);
    }


    public static SimpleError? GetSimpleError(this Event ev)
    {
        if (!ev.HasSimpleError())
            return null;

        try
        {
            return ev.Data!.GetValue<SimpleError>(Event.KnownDataKeys.SimpleError);
        }
        catch (Exception) { }

        return null;
    }

    public static RequestInfo? GetRequestInfo(this Event ev)
    {
        if (ev.Data is null || !ev.Data.ContainsKey(Event.KnownDataKeys.RequestInfo))
            return null;

        try
        {
            return ev.Data.GetValue<RequestInfo>(Event.KnownDataKeys.RequestInfo);
        }
        catch (Exception) { }

        return null;
    }

    public static EnvironmentInfo? GetEnvironmentInfo(this Event ev)
    {
        if (ev.Data is null || !ev.Data.ContainsKey(Event.KnownDataKeys.EnvironmentInfo))
            return null;

        try
        {
            return ev.Data.GetValue<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo);
        }
        catch (Exception) { }

        return null;
    }

    public static TagSet RemoveExcessTags(this TagSet tags)
    {
        tags.Trim(
            t => String.IsNullOrEmpty(t) || t.Length > 100,
            t => String.Equals(t, Event.KnownTags.Critical, StringComparison.OrdinalIgnoreCase) || String.Equals(t, Event.KnownTags.Internal, StringComparison.OrdinalIgnoreCase),
            50);

        return tags;
    }

    /// <summary>
    /// Indicates whether the event has been marked as critical.
    /// </summary>
    public static bool IsCritical(this Event ev)
    {
        return ev.Tags is not null && ev.Tags.Contains(Event.KnownTags.Critical);
    }

    /// <summary>
    /// Marks the event as being a critical occurrence.
    /// </summary>
    public static void MarkAsCritical(this Event ev)
    {
        ev.Tags ??= [];
        ev.Tags.Add(Event.KnownTags.Critical);
        ev.Tags.RemoveExcessTags();
    }

    /// <summary>
    /// Returns true if the event type is not found.
    /// </summary>
    public static bool IsNotFound(this Event ev)
    {
        return ev.Type == Event.KnownTypes.NotFound;
    }

    /// <summary>
    /// Returns true if the event type is error.
    /// </summary>
    public static bool IsError(this Event ev)
    {
        return ev.Type == Event.KnownTypes.Error;
    }

    /// <summary>
    /// Returns true if the event type is log.
    /// </summary>
    public static bool IsLog(this Event ev)
    {
        return ev.Type == Event.KnownTypes.Log;
    }

    /// <summary>
    /// Returns true if the event type is feature usage.
    /// </summary>
    public static bool IsFeatureUsage(this Event ev)
    {
        return ev.Type == Event.KnownTypes.FeatureUsage;
    }

    /// <summary>
    /// Returns true if the event type is session heartbeat.
    /// </summary>
    public static bool IsSessionHeartbeat(this Event ev)
    {
        return ev.Type == Event.KnownTypes.SessionHeartbeat;
    }

    /// <summary>
    /// Returns true if the event type is session start.
    /// </summary>
    public static bool IsSessionStart(this Event ev)
    {
        return ev.Type == Event.KnownTypes.Session;
    }

    /// <summary>
    /// Returns true if the event type is session end.
    /// </summary>
    public static bool IsSessionEnd(this Event ev)
    {
        return ev.Type == Event.KnownTypes.SessionEnd;
    }

    /// <summary>
    /// Adds the request info to the event.
    /// </summary>
    public static void AddRequestInfo(this Event ev, RequestInfo request)
    {
        ev.Data ??= new DataDictionary();
        ev.Data[Event.KnownDataKeys.RequestInfo] = request;
    }

    /// <summary>
    /// Gets the user info object from extended data.
    /// </summary>
    public static UserInfo? GetUserIdentity(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.UserInfo, out object? value) ? value as UserInfo : null;
    }

    public static string? GetVersion(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.Version, out object? value) ? value as string : null;
    }

    /// <summary>
    /// Sets the version that the event happened on.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="version">The version.</param>
    public static void SetVersion(this Event ev, string? version)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(version))
            ev.Data.Remove(Event.KnownDataKeys.Version);
        else
            ev.Data[Event.KnownDataKeys.Version] = version.Trim();
    }

    public static SubmissionClient? GetSubmissionClient(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.SubmissionClient, out object? value) ? value as SubmissionClient : null;
    }

    public static bool HasLocation(this Event ev)
    {
        return ev.Data != null && ev.Data.ContainsKey(Event.KnownDataKeys.Location);
    }

    public static Location? GetLocation(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.Location, out object? value) ? value as Location : null;
    }

    public static string? GetLevel(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.Level, out object? value) ? value as string : null;
    }

    public static void SetLevel(this Event ev, string level)
    {
        ev.Data ??= new DataDictionary();
        ev.Data[Event.KnownDataKeys.Level] = level;
    }

    public static string? GetSubmissionMethod(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.SubmissionMethod, out object? value) ? value as string : null;
    }

    public static void SetSubmissionClient(this Event ev, SubmissionClient client)
    {
        ev.Data ??= new DataDictionary();
        ev.Data[Event.KnownDataKeys.SubmissionClient] = client;
    }

    public static void SetLocation(this Event ev, Location? location)
    {
        ev.Data ??= new DataDictionary();
        if (location is null)
            ev.Data.Remove(Event.KnownDataKeys.Location);
        else
            ev.Data[Event.KnownDataKeys.Location] = location;
    }

    public static void SetEnvironmentInfo(this Event ev, EnvironmentInfo? environmentInfo)
    {
        ev.Data ??= new DataDictionary();
        if (environmentInfo is null)
            ev.Data.Remove(Event.KnownDataKeys.EnvironmentInfo);
        else
            ev.Data[Event.KnownDataKeys.EnvironmentInfo] = environmentInfo;
    }

    /// <summary>
    /// Gets the stacking info from extended data.
    /// </summary>
    public static ManualStackingInfo? GetManualStackingInfo(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.ManualStackingInfo, out object? value) ? value as ManualStackingInfo : null;
    }

    /// <summary>
    /// Changes default stacking behavior
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="signatureData">Key value pair that determines how the event is stacked.</param>
    public static void SetManualStackingInfo(this Event ev, IDictionary<string, string> signatureData)
    {
        ev.Data ??= new DataDictionary();
        if (signatureData.Count == 0)
            ev.Data.Remove(Event.KnownDataKeys.ManualStackingInfo);
        else
            ev.Data[Event.KnownDataKeys.ManualStackingInfo] = new ManualStackingInfo(signatureData);
    }

    /// <summary>
    /// Changes default stacking behavior
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="title">The stack title.</param>
    /// <param name="signatureData">Key value pair that determines how the event is stacked.</param>
    public static void SetManualStackingInfo(this Event ev, string title, IDictionary<string, string> signatureData)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(title) || signatureData is null || signatureData.Count == 0)
            ev.Data.Remove(Event.KnownDataKeys.ManualStackingInfo);
        else
            ev.Data[Event.KnownDataKeys.ManualStackingInfo] = new ManualStackingInfo(title, signatureData);
    }

    /// <summary>
    /// Changes default stacking behavior by setting the stacking info.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="manualStackingKey">The manual stacking key.</param>
    public static void SetManualStackingKey(this Event ev, string manualStackingKey)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(manualStackingKey))
            ev.Data.Remove(Event.KnownDataKeys.ManualStackingInfo);
        else
            ev.Data[Event.KnownDataKeys.ManualStackingInfo] = new ManualStackingInfo(null, new Dictionary<string, string> { { "ManualStackingKey", manualStackingKey } });
    }

    /// <summary>
    /// Changes default stacking behavior by setting the stacking info.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="title">The stack title.</param>
    /// <param name="manualStackingKey">The manual stacking key.</param>
    public static void SetManualStackingKey(this Event ev, string title, string manualStackingKey)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(title) || String.IsNullOrWhiteSpace(manualStackingKey))
            ev.Data.Remove(Event.KnownDataKeys.ManualStackingInfo);
        else
            ev.Data[Event.KnownDataKeys.ManualStackingInfo] = new ManualStackingInfo(title, new Dictionary<string, string> { { "ManualStackingKey", manualStackingKey } });
    }

    /// <summary>
    /// Sets the user's identity (ie. email address, username, user id) that the event happened to.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="identity">The user's identity that the event happened to.</param>
    public static void SetUserIdentity(this Event ev, string identity)
    {
        ev.SetUserIdentity(identity, null);
    }

    /// <summary>
    /// Sets the user's identity (ie. email address, username, user id) and name that the event happened to.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="identity">The user's identity that the event happened to.</param>
    /// <param name="name">The user's friendly name that the event happened to.</param>
    public static void SetUserIdentity(this Event ev, string identity, string? name)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(identity) && String.IsNullOrWhiteSpace(name))
            ev.Data.Remove(Event.KnownDataKeys.UserInfo);
        else
            ev.SetUserIdentity(new UserInfo(identity, name));
    }

    /// <summary>
    /// Sets the user's identity (ie. email address, username, user id) and name that the event happened to.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="userInfo">The user's identity that the event happened to.</param>
    public static void SetUserIdentity(this Event ev, UserInfo? userInfo)
    {
        ev.Data ??= new DataDictionary();
        if (userInfo is null)
            ev.Data.Remove(Event.KnownDataKeys.UserInfo);
        else
            ev.Data[Event.KnownDataKeys.UserInfo] = userInfo;
    }

    public static void RemoveUserIdentity(this Event ev)
    {
        ev.Data?.Remove(Event.KnownDataKeys.UserInfo);
    }

    /// <summary>
    /// Gets the user description from extended data.
    /// </summary>
    public static UserDescription? GetUserDescription(this Event ev)
    {
        return ev.Data != null && ev.Data.TryGetValue(Event.KnownDataKeys.UserDescription, out object? value) ? value as UserDescription : null;
    }

    /// <summary>
    /// Sets the user's description of the event.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="emailAddress">The email address</param>
    /// <param name="description">The user's description of the event.</param>
    public static void SetUserDescription(this Event ev, string emailAddress, string description)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(emailAddress) && String.IsNullOrWhiteSpace(description))
            ev.Data.Remove(Event.KnownDataKeys.UserDescription);
        else
            ev.Data[Event.KnownDataKeys.UserDescription] = new UserDescription(emailAddress, description);
    }

    /// <summary>
    /// Sets the user's description of the event.
    /// </summary>
    /// <param name="ev">The event.</param>
    /// <param name="description">The user's description.</param>
    public static void SetUserDescription(this Event ev, UserDescription description)
    {
        ev.Data ??= new DataDictionary();
        if (String.IsNullOrWhiteSpace(description.EmailAddress) && String.IsNullOrWhiteSpace(description.Description))
            ev.Data.Remove(Event.KnownDataKeys.UserDescription);
        else
            ev.Data[Event.KnownDataKeys.UserDescription] = description;
    }

    public static byte[] GetBytes(this Event ev, JsonSerializerSettings settings)
    {
        return Encoding.UTF8.GetBytes(ev.ToJson(Formatting.None, settings));
    }
}
