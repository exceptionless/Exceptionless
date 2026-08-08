using Exceptionless.Core.Models.Data;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Api.Messages;

// Count messages
public record GetEventCount(string? Filter, string? Aggregations, string? Time, string? Offset, string? Mode, HttpContext Context);
public record GetEventCountByOrganization(string OrganizationId, string? Filter, string? Aggregations, string? Time, string? Offset, string? Mode, HttpContext Context);
public record GetEventCountByProject(string ProjectId, string? Filter, string? Aggregations, string? Time, string? Offset, string? Mode, HttpContext Context);

// Get events
public record GetEventById(string Id, string? ExpectedStackId, string? Time, string? Offset, HttpContext Context);
public record GetAllEvents(string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsByOrganization(string OrganizationId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsByProject(string ProjectId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsByStack(string StackId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsByReferenceId(string ReferenceId, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsByReferenceIdAndProject(string ReferenceId, string ProjectId, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);

// Sessions
public record GetEventsBySessionId(string SessionId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetEventsBySessionIdAndProject(string SessionId, string ProjectId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetSessions(string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetSessionsByOrganization(string OrganizationId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);
public record GetSessionsByProject(string ProjectId, string? Filter, string? Sort, string? Time, string? Offset, string? Mode, int? Page, int Limit, string? Before, string? After, string? Include, HttpContext Context);

// User description
public record SetEventUserDescription(string ReferenceId, UserDescription Description, string? ProjectId, HttpContext Context);
public record LegacyPatchEvent(string Id, Delta<UpdateEvent> Changes, HttpContext Context);

// Heartbeat
public record RecordEventHeartbeat(string? Id, bool Close, HttpContext Context);

// Submit via GET
public record SubmitEventByGet(string? ProjectId, int ApiVersion, string? Type, string? UserAgent, HttpContext Context);

// Submit via POST
public record SubmitEventByPost(string? ProjectId, int ApiVersion, string? UserAgent, bool TrackProcessing, HttpContext Context);

// Delete
public record DeleteEvents(string Ids, HttpContext Context);
