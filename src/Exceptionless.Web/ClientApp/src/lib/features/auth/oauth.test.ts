import { describe, expect, it } from 'vitest';

import { formatOAuthScope } from './oauth';

describe('formatOAuthScope', () => {
    it.each([
        ['events:read', 'Events Read'],
        ['mcp:read', 'MCP'],
        ['offline_access', 'Offline Access'],
        ['projects:read', 'Projects Read'],
        ['stacks:read', 'Stacks Read'],
        ['stacks:write', 'Stacks Write'],
        ['custom:scope', 'custom:scope']
    ])('formats %s as %s', (scope, expected) => {
        expect(formatOAuthScope(scope)).toBe(expected);
    });
});
