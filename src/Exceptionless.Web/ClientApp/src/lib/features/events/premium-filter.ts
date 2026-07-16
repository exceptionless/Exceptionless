export type SearchResource = 'event' | 'stack';

// These mirror the backend query validators so the upgrade notification is shown
// before a restricted request fails. The API remains the enforcement boundary.
const FREE_QUERY_FIELDS: Record<SearchResource, ReadonlySet<string>> = {
    event: new Set(['date', 'organization', 'organization_id', 'project', 'project_id', 'reference', 'reference_id', 'stack', 'stack_id', 'status', 'type']),
    stack: new Set([
        'critical',
        'first',
        'first_occurrence',
        'last',
        'last_occurrence',
        'occurrences_are_critical',
        'organization',
        'organization_id',
        'project',
        'project_id',
        'status',
        'type'
    ])
};

/**
 * Returns true if the filter string references fields that require a premium plan.
 * Uses client-side field detection to avoid an extra API call.
 */
export function filterUsesPremiumFeatures(filter: null | string | undefined, resource: SearchResource): boolean {
    if (!filter) {
        return false;
    }

    const fields = extractFilterFields(filter);
    return fields.some((field) => !FREE_QUERY_FIELDS[resource].has(field.toLowerCase()));
}

/**
 * Extracts field names from a Lucene-style filter string.
 * Matches patterns like `field:value` or `field:(value1 OR value2)`.
 */
function extractFilterFields(filter: string): string[] {
    const fieldPattern = /(?:^|\s|[(!])[-+]?(\w[\w.@]*):/g;
    const fields: string[] = [];
    let match: null | RegExpExecArray;

    while ((match = fieldPattern.exec(filter)) !== null) {
        fields.push(match[1]!);
    }

    return fields;
}
