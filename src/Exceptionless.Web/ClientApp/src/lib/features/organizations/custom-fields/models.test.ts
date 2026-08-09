import { describe, expect, it } from 'vitest';

import { parseApiIndexType } from './models';
import { CustomFieldNameSchema } from './schemas';

describe('parseApiIndexType', () => {
    it('accepts supported server values', () => {
        expect(parseApiIndexType('keyword')).toBe('keyword');
        expect(parseApiIndexType('long')).toBe('long');
    });

    it('fails loudly for an unknown server value', () => {
        expect(() => parseApiIndexType('decimal')).toThrow("Unsupported custom-field index type 'decimal'.");
    });
});

describe('CustomFieldNameSchema', () => {
    it.each(['keyword-7', 'bool-1', 'session-r', 'sessionend-d', 'haserror-b'])('rejects internal storage name %s', (name) => {
        expect(CustomFieldNameSchema.safeParse(name).success).toBe(false);
    });
});
