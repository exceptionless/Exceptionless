import type { CountResult } from '$generated/api';

export enum MigrationType {
    Versioned = 0,
    VersionedAndResumable = 1,
    Repeatable = 2
}

export type AdminStats = {
    events: CountResult;
    organizations: CountResult;
    projects: CountResult;
    stacks: CountResult;
    users: CountResult;
};

export type ElasticsearchHealth = {
    active_primary_shards: number;
    active_shards: number;
    cluster_name: string;
    number_of_data_nodes: number;
    number_of_nodes: number;
    relocating_shards: number;
    status: number;
    unassigned_shards: number;
};

export type ElasticsearchIndexDetail = {
    docs_count: number;
    health?: null | string;
    index?: null | string;
    primary: number;
    replica: number;
    status?: null | string;
    store_size_in_bytes: number;
    unassigned_shards: number;
};

export type ElasticsearchIndices = {
    count: number;
    docs_count: number;
    store_size_in_bytes: number;
};

export type ElasticsearchInfo = {
    health: ElasticsearchHealth;
    index_details: ElasticsearchIndexDetail[];
    indices: ElasticsearchIndices;
};

export type ElasticsearchSnapshot = {
    duration: string;
    end_time?: null | string;
    failed_shards: number;
    indices_count: number;
    name: string;
    repository: string;
    start_time?: null | string;
    status: string;
    successful_shards: number;
    total_shards: number;
};

export type ElasticsearchSnapshotsResponse = {
    repositories: string[];
    snapshots: ElasticsearchSnapshot[];
};

export type MaintenanceAction = {
    category: MaintenanceActionCategory;
    dangerous: boolean;
    description: string;
    hasDateRange?: boolean;
    hasOrganizationId?: boolean;
    label: string;
    name: string;
};

export type MaintenanceActionCategory = 'Billing' | 'Configuration' | 'Elasticsearch' | 'Maintenance' | 'Security' | 'Users';
export type MigrationsResponse = {
    current_version: number;
    states: MigrationState[];
};

export type MigrationState = {
    completed_utc?: null | string;
    error_message?: null | string;
    id: string;
    migration_type: number;
    started_utc?: null | string;
    version: number;
};

export type MigrationStatus = 'Completed' | 'Failed' | 'Pending' | 'Running';

export type ShardMetric = {
    id: string;
    label: string;
    value: number;
};

export const maintenanceActions: MaintenanceAction[] = [
    {
        category: 'Elasticsearch',
        dangerous: false,
        description:
            'Runs Elasticsearch index setup for all indices: creates any missing indices and applies current field mappings. Does not reindex existing documents. Safe to run at any time to bring the schema in sync with new mapping definitions.',
        label: 'Configure Indexes',
        name: 'indexes'
    },
    {
        category: 'Billing',
        dangerous: false,
        description:
            'Re-applies the current billing plan limits and features (event limits, data retention, team size, etc.) to every organization without changing subscription status. Run after updating a plan definition to propagate changes to all existing subscribers.',
        label: 'Update Organization Plans',
        name: 'update-organization-plans'
    },
    {
        category: 'Maintenance',
        dangerous: true,
        description:
            'Permanently deletes hourly usage records older than 3 days and monthly usage records older than 366 days from every organization. Reduces document size and removes stale data no longer needed for billing or dashboards.',
        label: 'Remove Old Organization Usage',
        name: 'remove-old-organization-usage'
    },
    {
        category: 'Configuration',
        dangerous: false,
        description:
            'Re-stamps the latest system-default user-agent bot-filter patterns onto every project and bumps the configuration version, forcing all Exceptionless clients to refresh their local settings on the next request.',
        label: 'Update Project Default Bot Lists',
        name: 'update-project-default-bot-lists'
    },
    {
        category: 'Configuration',
        dangerous: false,
        description:
            'Bumps the configuration version counter on every project, forcing all connected Exceptionless clients to re-download their project settings (rate limits, user-agent filters, custom data exclusions, etc.) on the next heartbeat.',
        label: 'Increment Project Configuration Version',
        name: 'increment-project-configuration-version'
    },
    {
        category: 'Maintenance',
        dangerous: true,
        description:
            'Permanently deletes hourly usage records older than 3 days and monthly usage records older than 366 days from every project. Similar to organization usage cleanup but operates at the per-project level.',
        label: 'Remove Old Project Usage',
        name: 'remove-old-project-usage'
    },
    {
        category: 'Users',
        dangerous: false,
        description:
            "Trims whitespace and lowercases every user's email address and full name. Fixes historical records created before strict normalization was enforced, ensuring consistent login lookups and deduplication.",
        label: 'Normalize User Email Addresses',
        name: 'normalize-user-email-address'
    },
    {
        category: 'Security',
        dangerous: true,
        description:
            'Generates a fresh random verification token and resets the expiration date for every unverified user account. Run before a bulk re-verification email campaign or after changing the token TTL policy. Does not send any emails.',
        label: 'Reset Verify Email Address Tokens',
        name: 'reset-verify-email-address-token-and-expiration'
    },
    {
        category: 'Elasticsearch',
        dangerous: false,
        description:
            'Re-derives first occurrence, last occurrence, and total event count for every stack by running aggregations against raw event documents. Only updates fields that are out-of-date. Accepts an optional date range and organization ID to limit scope. Corrects stale or corrupted stats caused by missed event counter flushes.',
        hasDateRange: true,
        hasOrganizationId: true,
        label: 'Fix Stack Stats',
        name: 'fix-stack-stats'
    },
    {
        category: 'Maintenance',
        dangerous: false,
        description:
            'Scans every project and removes notification settings for users who no longer belong to the organization. Accepts an optional organization ID to limit scope. Prevents stale user entries from accumulating in project notification settings.',
        hasOrganizationId: true,
        label: 'Update Project Notification Settings',
        name: 'update-project-notification-settings'
    }
];
