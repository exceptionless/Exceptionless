﻿using System.Diagnostics;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id: {Id}, Type: {Type}, Date: {Date}, Message: {Message}, Value: {Value}, Count: {Count}")]
public class PersistentEvent : Event, IOwnedByOrganizationAndProjectAndStackWithIdentity, IHaveCreatedDate
{
    /// <summary>
    /// Unique id that identifies an event.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The organization that the event belongs to.
    /// </summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// The project that the event belongs to.
    /// </summary>
    public string ProjectId { get; set; } = null!;

    /// <summary>
    /// The stack that the event belongs to.
    /// </summary>
    public string StackId { get; set; } = null!;

    /// <summary>
    /// Whether the event resulted in the creation of a new stack.
    /// </summary>
    public bool IsFirstOccurrence { get; set; }

    /// <summary>
    /// The date that the event was created in the system.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Used to store primitive data type custom data values for searching the event.
    /// </summary>
    public DataDictionary Idx { get; set; } = new();
}
