import { describe, expect, it } from 'vitest';

import { getOAuthErrorMessage } from './oauth-error';

describe('OAuth error messages', () => {
    it.each([
        [{ error_description: 'OAuth description' }, 'OAuth description'],
        [{ error: 'invalid_scope' }, 'invalid_scope'],
        [{ detail: 'Problem detail' }, 'Problem detail'],
        [{ title: 'Problem title' }, 'Problem title'],
        [{ detail: 7, error: [], error_description: {}, title: false }, 'Fallback'],
        [{ detail: 'Problem detail', error_description: '  ' }, 'Problem detail'],
        [null, 'Fallback'],
        ['Malformed body', 'Fallback']
    ])('handles OAuth, ordinary problem details, and malformed bodies: %j', (problem, expected) => {
        expect(getOAuthErrorMessage({ data: null, problem }, 'Fallback')).toBe(expected);
    });

    it('retains the description from a parsed response body', () => {
        expect(getOAuthErrorMessage({ data: { error_description: 'Description' }, problem: { title: 'Title' } }, 'Fallback')).toBe('Description');
    });
});
