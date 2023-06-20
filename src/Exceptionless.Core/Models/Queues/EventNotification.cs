﻿namespace Exceptionless.Core.Queues.Models;

public class EventNotification
{
    public string EventId { get; set; }
    public bool IsNew { get; set; }
    public bool IsRegression { get; set; }
    public int TotalOccurrences { get; set; }
}
