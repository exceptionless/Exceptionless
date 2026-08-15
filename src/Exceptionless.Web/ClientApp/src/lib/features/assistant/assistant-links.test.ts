import { describe, expect, it } from 'vitest';

import { normalizeAssistantUrl } from './assistant-links';

describe('normalizeAssistantUrl', () => {
    it('converts absolute Exceptionless app routes to same-origin paths', () => {
        expect(normalizeAssistantUrl('https://exceptionless.local/next/stack/stack-id?mode=summary#event', 'href')).toBe(
            '/next/stack/stack-id?mode=summary#event'
        );
    });

    it('leaves relative and genuinely external links unchanged', () => {
        expect(normalizeAssistantUrl('/next/stack/stack-id', 'href')).toBe('/next/stack/stack-id');
        expect(normalizeAssistantUrl('https://docs.exceptionless.com/product/errors', 'href')).toBe('https://docs.exceptionless.com/product/errors');
    });

    it('does not rewrite image sources', () => {
        const source = 'https://example.com/next/assets/chart.png';
        expect(normalizeAssistantUrl(source, 'src')).toBe(source);
    });
});
