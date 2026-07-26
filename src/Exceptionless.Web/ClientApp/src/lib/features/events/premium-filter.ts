export type SearchResource = 'event' | 'event-stack' | 'stack';

// Alias and indexed-field variants intentionally mirror the backend validators.
const EVENT_FREE_QUERY_FIELDS = new Set([
    'date',
    'organization',
    'organization_id',
    'project',
    'project_id',
    'ref.parent',
    'reference',
    'reference_id',
    'stack',
    'stack_id',
    'status',
    'type'
]);
const STACK_FREE_QUERY_FIELDS = new Set([
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
]);

// These mirror the backend event, stack-mode event, and direct stack rules.
// The API remains the enforcement boundary.
const FREE_QUERY_FIELDS: Record<SearchResource, ReadonlySet<string>> = {
    event: EVENT_FREE_QUERY_FIELDS,
    'event-stack': new Set([...EVENT_FREE_QUERY_FIELDS, ...STACK_FREE_QUERY_FIELDS]),
    stack: STACK_FREE_QUERY_FIELDS
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

export function getSearchResourceForPathname(pathname: string): SearchResource {
    if (/\/project\/[^/]+\/stacks(?:\/|$)/.test(pathname)) {
        return 'stack';
    }

    return /(?:^|\/)stack(?:\/|$)/.test(pathname) ? 'event-stack' : 'event';
}

/**
 * Extracts field names from a Lucene-style filter string.
 * Matches patterns like `field:value` or `field:(value1 OR value2)`.
 */
function extractFilterFields(filter: string): string[] {
    // Ignore quoted and range values so value text such as ISO timestamps is not mistaken for a field.
    const filterWithoutValueLiterals = filter.replace(/"(?:\\.|[^"\\])*"|[[{](?:\\.|[^}\]\\])*[\]}]/g, '');
    // Existence queries name their field after the operator; other fields may have unary +/- prefixes and contain metadata (@), Unicode, or custom-name hyphens.
    const fieldPattern = /(?:^|\s|[(!])[-+]?(?:(?:_exists_|_missing_):([\p{L}\p{N}_][\p{L}\p{N}_.@-]*)|([\p{L}\p{N}_][\p{L}\p{N}_.@-]*):)/giu;
    const fields: string[] = [];
    let match: null | RegExpExecArray;

    while ((match = fieldPattern.exec(filterWithoutValueLiterals)) !== null) {
        fields.push(match[1] ?? match[2]!);
    }

    return fields;
}
