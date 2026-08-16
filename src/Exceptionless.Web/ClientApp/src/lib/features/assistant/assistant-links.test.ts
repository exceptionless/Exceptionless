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

    it('preserves complete inline-link destinations with balanced parentheses', () => {
        const content = '[docs](https://example.test/(guide)/API)';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(content);
    });

    it('preserves resource labels inside indented code blocks', () => {
        const content = `    API.connect()

Use API.`;

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(`    API.connect()

Use [API](/next/project/api).`);
    });

    it('preserves multi-backtick code spans containing shorter backtick runs', () => {
        const content = '``code ` API`` and API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(
            '``code ` API`` and [API](/next/project/api).'
        );
    });

    it('preserves fenced code when the delimiter occurs mid-line', () => {
        const content = ['```ts', 'const marker = "```";', 'API.connect()', '```', '', 'Use API.'].join('\n');
        const expected = ['```ts', 'const marker = "```";', 'API.connect()', '```', '', 'Use [API](/next/project/api).'].join('\n');

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(expected);
    });

    it('preserves fenced code nested inside blockquotes', () => {
        const content = ['> ```ts', '> API.connect()', '> ```', '', 'Use API.'].join('\n');
        const expected = ['> ```ts', '> API.connect()', '> ```', '', 'Use [API](/next/project/api).'].join('\n');

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(expected);
    });

    it('preserves resource labels inside email addresses', () => {
        const content = 'Contact API@example.com before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(
            'Contact API@example.com before opening [API](/next/project/api).'
        );
    });

    it('preserves reference-style and shortcut links', () => {
        const content = `[Timeout expired][stack], [Timeout expired][], and [Timeout expired] are already linked.

[stack]: /next/stack/existing`;

        expect(addAssistantResourceLinks(content, [toolResult([{ title: 'Timeout expired', webUrl: '/next/stack/stack-1' }])])).toBe(content);
    });

    it('links only complete resource labels', () => {
        const content = 'APIClient uses the API project.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(
            'APIClient uses the [API](/next/project/api) project.'
        );
    });

    it('preserves labels inside bare absolute and relative URLs', () => {
        const content = 'See https://example.test/API and /next/project/API before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/next/project/api' }])])).toBe(
            'See https://example.test/API and /next/project/API before opening [API](/next/project/api).'
        );
    });

    it('ignores punctuation-only resource labels', () => {
        const content = `- Keep this list item

| Name | Count |
| --- | --- |`;

        expect(addAssistantResourceLinks(content, [toolResult([{ name: '-', webUrl: '/next/project/dash' }])])).toBe(content);
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
