import { describe, expect, it } from 'vitest';

import type { AssistantToolActivity } from './models';

import { addAssistantResourceLinks, normalizeAssistantUrl } from './assistant-links';

function toolResult(items: unknown[]): AssistantToolActivity {
    return {
        arguments: '{}',
        id: 'tool-call',
        name: 'search_stacks',
        result: JSON.stringify({ data: { items }, ok: true }),
        status: 'complete'
    };
}

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

describe('addAssistantResourceLinks', () => {
    it('links matching stack titles in tables and prose using tool-result web URLs', () => {
        const content = `| Type | Title |
| --- | --- |
| Error | Connection refused (localhost:9200) |

**Connection refused (localhost:9200)** is the most active stack.`;

        const result = addAssistantResourceLinks(content, [
            toolResult([{ id: 'stack-1', title: 'Connection refused (localhost:9200)', webUrl: '/next/stack/stack-1' }])
        ]);

        expect(result).toContain('| Error | [Connection refused (localhost:9200)](/next/stack/stack-1) |');
        expect(result).toContain('**[Connection refused (localhost:9200)](/next/stack/stack-1)**');
    });

    it('preserves existing links and code examples', () => {
        const content = `[Timeout expired](/next/stack/existing) is already linked.

\`Timeout expired\`

\`\`\`text
Timeout expired
\`\`\``;

        expect(addAssistantResourceLinks(content, [toolResult([{ title: 'Timeout expired', webUrl: '/next/stack/stack-1' }])])).toBe(content);
    });

    it('does not guess when duplicate titles refer to different resources', () => {
        const content = 'Investigate Timeout expired.';
        const tools = [
            toolResult([
                { title: 'Timeout expired', webUrl: '/next/stack/stack-1' },
                { title: 'Timeout expired', webUrl: '/next/stack/stack-2' }
            ])
        ];

        expect(addAssistantResourceLinks(content, tools)).toBe(content);
    });

    it('ignores nested and external URLs from untrusted result data', () => {
        const content = 'Do not link this title.';
        const tools = [
            toolResult([
                {
                    data: { title: 'this title', webUrl: '/next/stack/untrusted' },
                    title: 'External title',
                    webUrl: 'https://example.com/stack/1'
                }
            ])
        ];

        expect(addAssistantResourceLinks(content, tools)).toBe(content);
    });
});
