import type { CountResult } from '$shared/models';

import { terms } from '$shared/api/aggregations';

export const TAG_SUGGESTION_LIMIT = 250;
export const TAG_SUGGESTION_STALE_TIME = 5 * 60 * 1000;

export function tagSuggestionAggregation(search: string): string {
    if (!search) {
        return `terms:(tags~${TAG_SUGGESTION_LIMIT + 1})`;
    }

    // Terms include uses Lucene regex, inside the aggregation parser's slash-delimited term.
    const literal = Array.from(search, (character) => {
        const lower = character.toLowerCase();
        const upper = character.toUpperCase();
        if (lower !== upper && Array.from(lower).length === 1 && Array.from(upper).length === 1) {
            return `[${lower}${upper}]`;
        }

        return /[.\\?+*|{}[\]()"#@&<>~]/u.test(character) ? `\\${character}` : character;
    }).join('');
    const pattern = literal.replaceAll('\\', '\\\\').replaceAll('/', '\\/');
    return `terms:(tags~${TAG_SUGGESTION_LIMIT} @include:/.*${pattern}.*/)`;
}

export function tagSuggestions(result: CountResult | undefined) {
    const aggregate = terms(result?.aggregations, 'terms_tags');
    const buckets = aggregate?.buckets ?? [];
    const data = aggregate?.data;
    // The repository adapter omits zero counters, but always marks its bucket metadata.
    const complete =
        !!aggregate &&
        data?.['@type'] === 'bucket' &&
        buckets.length <= TAG_SUGGESTION_LIMIT &&
        (data.SumOtherDocCount === undefined || data.SumOtherDocCount === 0) &&
        (data.DocCountErrorUpperBound === undefined || data.DocCountErrorUpperBound === 0);

    return { complete, tags: buckets.slice(0, TAG_SUGGESTION_LIMIT).map((bucket) => bucket.key) };
}

// Authentication redirects invalidate queries but do not always clear the shared cache.
// Partition suggestions without including credentials in query keys; retain only the latest token.
let lastToken: null | string | undefined;
let session = 0;
export function tagSuggestionSession(token: null | string): number {
    if (token !== lastToken) {
        lastToken = token;
        session++;
    }

    return session;
}
