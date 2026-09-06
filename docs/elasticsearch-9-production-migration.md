# Elasticsearch 9 production migration plan

Status: proposed; no production changes have been made. Inventory observations below are from September 6, 2026. This is an operator-run migration, not an application-startup migration.

**Stack PR #2511 is an experiment only, not the production implementation or a planned production release.** Its queries, endpoint changes, and lookup schema are proof-of-concept evidence, not an approved architecture. The proper stack/event query refactor still needs to be designed and implemented in the Foundatio.Repositories PR, then integrated into Exceptionless through a separately reviewed application change. Neither the Elasticsearch 9 server upgrade nor index-format maintenance depends on shipping #2511.

## Separate the three changes

| Change | Release boundary | Required data work |
| --- | --- | --- |
| Run Elasticsearch 9 with existing application queries | Base PR [#2416](https://github.com/exceptionless/Exceptionless/pull/2416) alone | Resolve unsupported pre-8 indexes before starting 9; do not rewrite all 8-created indexes |
| Recreate indexes in the current server's index format | Explicit maintenance after the server upgrade stabilizes | Selected retained indexes, independently of application schema versions |
| Properly refactor stack/event queries and stack pagination | Future repositories-led implementation and separately reviewed Exceptionless integration; [#2511](https://github.com/exceptionless/Exceptionless/pull/2511) is experimental evidence only | Determine index/migration requirements from the approved repository design; validate mixed-generation event sources and production-scale queries |

An Elasticsearch server version, `index.version.created`, the repository's schema version, and `index.mode` are different things. A force merge or server restart is not an index recreation. Do not bump the daily event schema version just to trigger a cluster-wide rewrite.

The base PR retains the application query/index schema behavior and the Elasticsearch 8 client compatibility bridge; it must run independently of the experimental query services. There is no application-level legacy/JOIN switch. Deploy the base application release independently. Do not deploy the experiment; any future query release must use the approved repositories implementation and satisfy its own migration and validation gates.

Elastic permits the supported previous-major index format on the next major. The documented LOOKUP JOIN constraint applies to the lookup-side index, not a blanket requirement to recreate every source event index. Confirm the exact expression joins used by the experiment against real 8-created event partitions on the target server before treating this as a production guarantee.

### Local evidence on the rebased PRs

Both solution builds passed with zero warnings/errors. The base PR independently passed all 829 API endpoint tests against the isolated custom Elasticsearch 9.5.0 image.

A separate disposable cluster created `migration-events-v1` on 8.19.15 (`index.version.created=8537000`), then started 9.5.0 on that same fixture volume without recreating the event index. A newly created `migration-stacks-v2` had lookup mode and creation version `9107000`. The following expression join returned the expected `[1, "stack-1"]` row with `is_partial=false`:

```esql
FROM migration-events-v1
| LOOKUP JOIN migration-stacks-v2 ON stack_id == id AND is_deleted == false AND QSTR("status:open")
| WHERE id IS NOT NULL
| STATS total = COUNT(*) BY stack_id
| SORT total DESC, stack_id ASC
| LIMIT 26
```

This proves mixed-generation support for that query shape, not production mapping coverage, throughput, or readiness. The restored production-data rehearsal remains required.

## Observed production topology and missing sizing evidence

- Production is green on Elasticsearch 8.19.15: four ready data/ingest/master nodes, each with 18 GiB container memory, a 9 GiB JVM heap, and a 600 GiB premium managed disk claim.
- Total provisioned storage is 2,400 GiB. This is **not** measured used storage or available migration headroom. Replica copies, shard placement, watermarks, growth, and recovery reserve all matter.
- The deployed ECK operator is 3.3.2. Production Kibana and the separate monitoring Elasticsearch cluster are on 8.19.15.
- The available Kubernetes identity can read resource metadata and logs, but cannot open an Elasticsearch port-forward or service proxy. Index creation versions, document counts, store sizes, shard distribution, and actual per-node free disk have **not** been measured. No duration or capacity promise is possible yet.

Obtain an existing read-only Elasticsearch connection or have an operator export the following. Use monitoring/metadata privileges, not write or reindex privileges. Run requests against the intended cluster and record the cluster UUID; do not put credentials or complete source documents in reports.

```http
GET /
GET /_cluster/health
GET /_cluster/settings?include_defaults=true&flat_settings=true
GET /_nodes/stats/fs,jvm,indices?filter_path=nodes.*.name,nodes.*.fs.total,nodes.*.jvm.mem,nodes.*.indices.indexing,nodes.*.indices.search,nodes.*.indices.merges
GET /_cat/allocation?format=json&bytes=b
GET /_cat/shards?format=json&bytes=b&h=index,shard,prirep,state,docs,store,node
GET /_cat/indices?format=json&bytes=b&expand_wildcards=all&h=index,health,status,pri,rep,docs.count,docs.deleted,pri.store.size,store.size
GET /_all/_settings?expand_wildcards=all&flat_settings=true&filter_path=*.settings.index.version.*,*.settings.index.mode,*.settings.index.number_of_*,*.settings.index.blocks.*,*.settings.index.lifecycle.*,*.settings.index.default_pipeline,*.settings.index.final_pipeline
GET /_all/_alias?expand_wildcards=all
GET /_migration/deprecations
GET /_snapshot/_all
GET /_slm/stats
```

System-index metadata may require an operator's separate access. Inventory those indexes through Upgrade Assistant rather than assuming application preflight covers them. Obtain latest successful snapshot details and a restore-test record; a configured repository alone does not prove a usable backup.

Build a manifest with one row per concrete index: canonical aliases, application owner, creation version, mode, date partition, primary bytes, total bytes, exact live document count for migration candidates, shard/replica counts, retention deadline, last observed writes, pipelines, and chosen action. CAT document counts can include nested documents; use `_count` for copy verification. Capture mappings for selected pilot/copy candidates, including `_source` availability and dynamic fields.

Measure normal and peak ingestion, search latency, disk growth, merge I/O, queue age, and recovery throughput over a representative workload window. Confirm configured maximum retention and actual cleanup behavior; do not assume an old daily partition is immutable or already expired.

## Phase 1: rehearsal and pre-upgrade work on 8.19

1. Pin the exact approved target patch and container digest. The PR currently pins 9.5.0; Elastic's current documented release is 9.5.3. Review intervening security fixes, known issues, plugins, client behavior, ECK support, and release-date upgrade compatibility before approving production. Test the same image that will be deployed, including the Exceptionless plugins. Do not silently substitute a production image during execution.
2. Patch Elasticsearch and Kibana to the latest approved 8.19 release first. Run Upgrade Assistant and resolve critical deprecations. Inventory all application, retained schema/error, hidden, monitoring, and system indexes.
3. Reindex any writable pre-8 application indexes **on Elasticsearch 8** before starting 9. Delete expired data only under the existing retention policy and explicit operator approval. Archive/read-only options are not replacements for writable Exceptionless indexes. Let Elastic's tooling own system-index migrations.
4. Restore a current snapshot to an isolated rehearsal cluster with matching topology/settings. Verify restore permissions, encryption keys, repository access, and recovery time. Restrict network access and apply production-data handling controls.
5. Run the base application on the rehearsed 8 cluster, upgrade that cluster to the target 9 patch, and rerun ingest, event/stack queries, stack status changes, jobs, saved views, aliases, retention, and deletion checks. Include existing records with old/missing fields and all retained creation versions.
6. As separate experimental research, create a lookup stack index in the rehearsal environment and test the experimental expression JOIN against **unchanged 8-created event indexes**. Compare status/deleted-stack filtering, tenant isolation, counts/charts, date boundaries, forward/backward cursors, and hydrated result identity. These findings inform the repositories PR; they neither approve the experiment for production nor block the independent base upgrade. Repeat correctness and performance validation against the eventual repository implementation.

No production load tests or reindex experiments are authorized by this plan.

## Phase 2: server upgrade with the base PR only

1. Deploy and soak the base-compatible application independently of the server change while keeping production infrastructure pinned to 8.19. Coordinate GitOps/release manifests so an application deployment cannot unintentionally apply the major-version infrastructure change.
2. Approve rollback RPO/RTO and the treatment of writes accepted after the backup boundary. Take a fresh successful snapshot and verify the restore procedure. If replay of post-snapshot events is required, demonstrate durable queue retention/replay and idempotency first; do not assume the current pipeline can recreate every stack/status mutation.
3. Upgrade the monitoring cluster and supporting components in the supported order before the monitored production cluster where required. Keep Kibana matched to its Elasticsearch version. Follow the ECK rolling-upgrade procedure; do not hand-delete pods or change shard allocation independently of the operator without an approved runbook.
4. Upgrade one production node at a time. Because all four nodes have the same roles, confirm voting quorum and recovery capacity with one node unavailable. Wait for each node's shard recovery and health before proceeding. Halt on sustained unassigned shards, disk watermark pressure, write/search errors, queue growth, or latency outside the agreed SLO.
5. Validate the base application against the upgraded cluster with its existing queries. Do not deploy #2511 or run compatibility reindexing during this initial stabilization window.

Rollback is **not** a downgrade of the upgraded disks or reverting the ECK version field. Recover on an older-version cluster from the pre-upgrade snapshot, with the approved replay/data-loss procedure. Preserve that recovery path until the upgrade has been accepted.

## Phase 3: upgrade index formats without rewriting the world at once

Prefer new daily event partitions created naturally on 9 and let eligible old partitions expire through verified retention. That upgrades the active data progressively with no bulk copy. Reindex only retained partitions that will outlive the agreed migration deadline or need a demonstrated format-specific feature. All remaining long-lived non-event indexes need an explicit current-format migration plan too.

Blake's [Foundatio.Repositories PR #307](https://github.com/FoundatioFx/Foundatio.Repositories/pull/307), reviewed at `aec4fb78652c1dae8d08085e5895e28fdf10a2a5`, is a promising operator primitive, but is still open and is not integrated into the base PR's package. It separates compatibility upgrades from normal schema/configuration startup. It preserves canonical aliases while replacing physical indexes and exposes inspection/recovery for interrupted operations.

Important constraints of that implementation:

- It fences writes for the entire copy and requires writers, consumers, retention/index maintenance, and alias managers to be stopped. It is not a zero-downtime CDC or dual-write solution.
- It copies one exact index into `reindexed-v9-<canonical-name>` using `_create_from` (an Elastic Technical Preview API), verifies the task, counts, mappings/settings, and alias topology, then atomically deletes the source and assigns its aliases to the target.
- Its copy task is unsliced and throttled. Do not size it assuming shard-parallel slicing. Partial/ambiguous operations require its evidence-based inspection/recovery, not blind retry or manual unblock.
- It rejects non-standard modes and several managed topologies. It preserves creation settings rather than changing them, so it does **not** implement the standard-to-lookup stack conversion.
- Canonical names become aliases. Prove exact-ID reads/patches, routing, mapping discovery, daily alias maintenance, old-schema discovery, retention deletion, and operational scripts still work. Restart/drain clients whose concurrency tokens refer to the retired physical index.

Before adoption, finish review/release of #307, consume the approved package in a separate maintenance change, and test its failure/recovery cases with Exceptionless. Do not hide it in `ConfigureIndexesAsync` or add an unconditional global schema bump.

The reviewed #307 scope is index-format maintenance. This plan does not claim that it already contains the proper stack query refactor. That query design and implementation remain work to resolve in the repositories PR, independently of the reindex primitive.

For each candidate:

1. Start with a small representative partition, then a larger/high-field-count partition. Measure sustained copy throughput **with** the intended production workload and throttle on the rehearsal cluster.
2. Reserve capacity for the complete target plus configured replica restoration, temporary segment/merge overhead, ingestion growth, and a node-recovery margin. Check fit per node and per shard against configured watermarks, not just aggregate free bytes. Provision additional capacity before copying if needed.
3. Stop all affected writers and index managers; drain in-flight work. Historical event dates can still receive late ingestion, deletions, and cleanup. A timestamp alone does not establish safety. If writers cannot be paused per partition with proven routing and queueing, the library's current safe procedure implies a broader maintenance outage. Decide that tradeoff explicitly before scheduling a massive copy.
4. Recompute the preflight; snapshot; copy only the approved concrete index with conservative throttling. Persist task identity, progress, baseline counts, aliases/settings, and recovery evidence outside the process.
5. Verify completion, exact counts, representative content/queries, replica recovery, alias identity, and application read/write/delete behavior. Release the write pause only after these gates pass. Then proceed to the next index; keep concurrency at one initially.
6. Abort/pause on the agreed disk/latency/queue limits. Use the library's inspection and verified cancellation path. A completed atomic cutover cannot be undone by canceling the task; snapshot restore is the recovery boundary.

Sizing worksheet: `copy duration ≈ primary source bytes / measured effective source-byte throughput`, plus refresh, validation, replica recovery, and cutover. Alternatively use exact documents divided by measured documents/second for matching document distributions. Estimate the **write-pause duration**, queue accumulation (`arrival rate × pause duration`), and catch-up time separately. Do not estimate from raw disk bandwidth or promise a universal 2× free-space rule.

## Phase 4: future repositories-led query refactor, not deployment of #2511

First settle the production query design in the Foundatio.Repositories PR: repository-level filtering/JOIN composition, grouped stack queries and counts, sorting/cursor semantics, result contracts, and index lifecycle support. Exceptionless should consume that capability rather than promote the experiment's application-side ES|QL service into the production architecture. Retain the intended endpoint ownership: stacks come from stacks endpoints, events from events endpoints, without application-side filtering joins.

Review and release that repository implementation, then create a separately reviewed Exceptionless integration and migration plan. #2511 is only a source of feasibility evidence and regression scenarios. Passing its tests is not approval to merge or deploy it as the production refactor.

The experiment defines stack schema version 2 with `index.mode=lookup` and one primary shard. These are provisional choices, not production migration instructions. The following requirements apply **only if the approved repositories design retains that lookup topology**; revise them if the design changes. Do not initiate this conversion through ordinary startup.

1. Measure the current stack primary size, document cardinality, growth, update rate, largest tenant, and heap needed by representative joins. The single lookup primary is a hard capacity/throughput constraint; replicas can distribute reads but do not shard primary writes. Establish whether this design fits the forecast, not just today's sample.
2. Use a dedicated, reviewed schema-conversion maintenance operation that creates the approved repository implementation's mapping/settings and copies existing stack documents without changing IDs or relationships. It must handle partial copies, missing/default fields, retained aliases, and rollback. #307's currently reviewed format upgrade is not this operation. Avoid copying stacks twice merely to reach current format and then lookup mode.
3. Pause ingestion consumers and all stack-mutating APIs/jobs/maintenance, drain in-flight writes, snapshot, copy and verify, then perform an explicit atomic alias cutover. Rehearse full outage/recovery behavior; do not assume the existing generic schema reindexer's catch-up pass proves no missed deletes or concurrent status changes.
4. Before allowing traffic, verify the actual lookup mode, mapping, one-primary setting, complete stack IDs/counts, aliases, replica health, and existing-record semantics. Deploy only the separately approved repository-backed application implementation after migration success, never the experimental PR. Keep the base release available as an application rollback candidate, but rehearse its writes against the new mapping; application rollback does not revert the index conversion.
5. Benchmark representative tenant/time-range/skew combinations for event status filtering and stack grouping/count/charts/paging. Capture p50/p95/p99 latency, CPU, heap/breakers, I/O, concurrent ingestion impact, and cursor correctness under changes. Cursor pagination does not remove the cost of filtering/grouping the qualifying event population, and it is not a point-in-time snapshot.
6. Require correctness and SLO acceptance before the JOIN production rollout. If the canonical one-primary stack index does not fit, stop and redesign the lookup topology; do not deploy on the strength of tiny local benchmarks.

## Decisions required before scheduling

- Read-only index/disk/snapshot inventory and representative workload measurements.
- Approved source/target patches, image digests, ECK/component compatibility, and rehearsal evidence.
- Retention-based completion deadline versus retained event partitions that must be copied.
- Additional capacity and acceptable per-index/global write outage, including queue/replay limits.
- Adoption of #307's reindex capability where needed; completion of the proper stack/event query refactor in the repositories PR, followed by separately reviewed Exceptionless integration and any required schema migration. #2511 is not a production deliverable.
- Restore-tested RPO/RTO, cutover/abort thresholds, and named operator/approval owner for each stage.

References: Elastic's [upgrade preparation](https://www.elastic.co/docs/deploy-manage/upgrade/prepare-to-upgrade), [rolling upgrade and rollback guidance](https://www.elastic.co/docs/deploy-manage/upgrade/deployment-or-cluster/elasticsearch), [LOOKUP JOIN constraints](https://www.elastic.co/docs/reference/query-languages/esql/esql-lookup-join), and [snapshot compatibility](https://www.elastic.co/docs/deploy-manage/tools/snapshot-and-restore).
