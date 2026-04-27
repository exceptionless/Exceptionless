/**
 * Free query fields that don't require a premium plan.
 * Any field referenced in a filter that is NOT in this set requires premium features.
 * Must be kept in sync with PersistentEventQueryValidator._freeQueryFields on the backend.
 */
const FREE_QUERY_FIELDS = new Set([
    'date',
    'organization',
    'organization_id',
    'project',
    'project_id',
    'reference',
    'reference_id',
    'stack',
    'stack_id',
    'status',
    'type'
]);

/**
 * Returns true if the filter string references fields that require a premium plan.
 * Uses client-side field detection to avoid an extra API call.
 */
export function filterUsesPremiumFeatures(filter: null | string | undefined): boolean {
    if (!filter) {
        return false;
    }

    const fields = extractFilterFields(filter);
    return fields.some((field) => !FREE_QUERY_FIELDS.has(field.toLowerCase()));
}

/**
 * Extracts field names from a Lucene-style filter string.
 * Matches patterns like `field:value` or `field:(value1 OR value2)`.
 */
function extractFilterFields(filter: string): string[] {
    const fieldPattern = /(?:^|\s|[(!])(\w[\w.]*):/g;
    const fields: string[] = [];
    let match: null | RegExpExecArray;

    while ((match = fieldPattern.exec(filter)) !== null) {
        fields.push(match[1]!);
    }

    return fields;
}
