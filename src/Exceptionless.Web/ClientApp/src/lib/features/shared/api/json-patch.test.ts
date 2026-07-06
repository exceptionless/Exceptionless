import { describe, expect, it } from 'vitest';

import { JSON_PATCH_CONTENT_TYPE, jsonPatchRequestOptions, toJsonPatch } from './json-patch';

describe('json-patch', () => {
    it('uses headers that preserve JSON response parsing for JSON Patch requests', () => {
        expect(jsonPatchRequestOptions.headers).toMatchObject({
            Accept: 'application/json, application/problem+json',
            'Content-Type': JSON_PATCH_CONTENT_TYPE
        });
    });

    it('converts defined values to replace operations', () => {
        expect(toJsonPatch({ delete_bot_data_enabled: undefined, name: 'Renamed', show_chart: false })).toEqual([
            { op: 'replace', path: '/name', value: 'Renamed' },
            { op: 'replace', path: '/show_chart', value: false }
        ]);
    });
});
