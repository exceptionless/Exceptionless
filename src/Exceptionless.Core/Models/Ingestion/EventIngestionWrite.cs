using Exceptionless.Core.Models;

namespace Exceptionless.Core.Models.Ingestion;

public sealed record EventIngestionWrite(
    string ClientId,
    PersistentEvent Event,
    StackFingerprint Fingerprint,
    StackRoute? Route);

public sealed record EventBatchWriteResult(int Persisted, int Duplicate);
