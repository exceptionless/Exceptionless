# Trial mailbox claims

## Purpose and proposed retention

Claims enforce one no-card trial per normalized mailbox. Account or organization deletion does not reset that eligibility. The record contains only a versioned keyed identifier and its creation time, with no email address, user ID, organization ID, IP address, or billing details. Treat it as restricted pseudonymous anti-abuse data, not anonymous data.

Proposed policy, pending Eric's confirmation on PR #2686: retain this minimal record for the lifetime of the one-trial-per-mailbox program, including after account deletion. There is no automatic TTL. Delete these records when that program is retired or when an approved erasure decision requires it; removing the claim makes that mailbox eligible again. A fixed retention period would instead intentionally permit another trial after expiry. Any future major-version retrial should be an explicit new eligibility program, not a secret change.

The public privacy notice and the handling of erasure requests must reflect the approved retention decision before rollout. This proposal does not establish a legal basis or resolve that policy approval.

## Dedicated secret and deployment

Set `Billing:TrialClaimSecret` (environment variable `EX_Billing__TrialClaimSecret`) to a dedicated base64-encoded secret containing at least 32 cryptographically random bytes. Generate it in the deployment's secret-management workflow, for example with `openssl rand -base64 32`. Store it separately from Elasticsearch, and supply the same value to every API, migration, and worker instance. Never commit it, reuse an authentication/Stripe secret, or log it. Configuration-object logging redacts this property.

Hosted trials refuse startup without this setting. If trials are disabled or billing is disabled, it may remain unset unless legacy claims need migration; no new claims/trials can be granted without a configured secret. Development mode's Unlimited plan remains available. A malformed or short configured secret is rejected.

Keep this secret stable and backed up for the program's lifetime. In-place secret rotation is not supported: a different secret produces different identifiers and could reset eligibility. A coordinated rotation needs its own design and migration; do not rotate by restarting with a newly generated value.

## Identifier and legacy migration

The identifier is `hmac-v1-` followed by the lowercase hex HMAC-SHA-256 of the normalized mailbox's SHA-256 digest, using the dedicated secret. The unkeyed digest is an intermediate in memory and is not retained in new claim records. This composition lets migration 12 protect pre-existing digest-only records without recovering email addresses.

Before rollout, configure the secret on all hosts and stop old-version trial admission. Do not run old unkeyed and new keyed claim writers concurrently. Migration 12 writes each keyed claim before deleting the old digest record, preserves its creation time, and tolerates an already-created target on resume. A missing key or failed write preserves the legacy record and fails the migration. Run migration before reopening signup. The PR's previous deployments may contain legacy claims even though the feature has not been merged.

The migration changes the active index. Existing backups or historical logs may still contain old digests until their own retention periods end; the migration does not retroactively erase those copies. Keep those copies restricted and account for them in the approved retention/erasure policy.

Claims use create-only repository insertion with the keyed identifier as the unique document ID. Duplicate claims return false; other storage failures abort admission. ID-based eligibility reads are real-time and do not require an immediate search refresh. Per-mailbox locks reduce contention; storage uniqueness is the final boundary.
