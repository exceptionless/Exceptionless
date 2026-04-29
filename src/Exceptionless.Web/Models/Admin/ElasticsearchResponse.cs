namespace Exceptionless.Web.Models.Admin;

public record ElasticsearchHealthResponse(
    int Status,
    string ClusterName,
    int NumberOfNodes,
    int NumberOfDataNodes,
    int ActiveShards,
    int RelocatingShards,
    int UnassignedShards,
    int ActivePrimaryShards
);

public record ElasticsearchIndicesResponse(
    long Count,
    long DocsCount,
    double StoreSizeInBytes
);

public record ElasticsearchIndexDetailResponse(
    string? Index,
    string? Health,
    string? Status,
    int Primary,
    int Replica,
    long DocsCount,
    long StoreSizeInBytes,
    int UnassignedShards
);

public record ElasticsearchInfoResponse(
    ElasticsearchHealthResponse Health,
    ElasticsearchIndicesResponse Indices,
    ElasticsearchIndexDetailResponse[]? IndexDetails
);

public record ElasticsearchSnapshotResponse(
    string Repository,
    string Name,
    string Status,
    DateTime? StartTime,
    DateTime? EndTime,
    string Duration,
    long IndicesCount,
    long SuccessfulShards,
    long FailedShards,
    long TotalShards
);

public record ElasticsearchSnapshotsResponse(
    string[]? Repositories,
    ElasticsearchSnapshotResponse[]? Snapshots
);
