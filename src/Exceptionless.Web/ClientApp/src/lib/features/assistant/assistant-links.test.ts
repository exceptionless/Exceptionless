import { beforeEach, describe, expect, it, vi } from 'vitest';

const paths = vi.hoisted(() => ({ base: '' }));

vi.mock('$app/paths', () => ({ resolve: (path: string) => `${paths.base}${path}` }));
vi.mock('$app/state', () => ({ page: { url: new URL('https://exceptionless.local/') } }));

import type { AssistantToolActivity } from './models';

import { addAssistantResourceLinks, normalizeAssistantUrl } from './assistant-links';

beforeEach(() => {
    paths.base = '';
});

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
        expect(normalizeAssistantUrl('https://exceptionless.local/stack/stack-id?mode=summary#event', 'href')).toBe('/stack/stack-id?mode=summary#event');
    });

    it('leaves relative and genuinely external links unchanged', () => {
        expect(normalizeAssistantUrl('/stack/stack-id', 'href')).toBe('/stack/stack-id');
        expect(normalizeAssistantUrl('https://docs.exceptionless.com/product/errors', 'href')).toBe('https://docs.exceptionless.com/product/errors');
    });

    it('does not rewrite image sources', () => {
        const source = 'https://example.com/assets/chart.png';
        expect(normalizeAssistantUrl(source, 'src')).toBe(source);
    });

    it('only rewrites same-origin absolute links when the app is hosted at root', () => {
        paths.base = '';

        expect(normalizeAssistantUrl('https://exceptionless.local/stack/stack-id?mode=summary#event', 'href')).toBe('/stack/stack-id?mode=summary#event');
        expect(normalizeAssistantUrl('https://docs.exceptionless.com/product/errors', 'href')).toBe('https://docs.exceptionless.com/product/errors');
        expect(normalizeAssistantUrl('https://example.com/stack/stack-id', 'href')).toBe('https://example.com/stack/stack-id');
        expect(normalizeAssistantUrl('/stack/stack-id', 'href')).toBe('/stack/stack-id');
    });

    it('does not confuse a similar prefix with an explicit app base', () => {
        paths.base = '/custom';
        expect(normalizeAssistantUrl('https://example.com/customdoor/stack/stack-id', 'href')).toBe('https://example.com/customdoor/stack/stack-id');
    });
});

describe('addAssistantResourceLinks', () => {
    it('links root-hosted resources while preserving existing URLs and markdown', () => {
        paths.base = '';
        const content = 'See /project/API and https://example.test/API, [API](/project/existing), and `API` before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api?tab=settings#details' }])])).toBe(
            'See /project/API and https://example.test/API, [API](/project/existing), and `API` before opening [API](/project/api?tab=settings#details).'
        );
    });

    it.each(['https://example.com/stack/1', '//example.com/stack/1', '/\\example.com/stack/1', '/\t/example.com/stack/1'])(
        'rejects external resource URL %s at root',
        (webUrl) => {
            paths.base = '';

            expect(addAssistantResourceLinks('Investigate this title.', [toolResult([{ title: 'this title', webUrl }])])).toBe('Investigate this title.');
        }
    );

    it('links matching stack titles in tables and prose using tool-result web URLs', () => {
        const content = `| Type | Title |
| --- | --- |
| Error | Connection refused (localhost:9200) |

**Connection refused (localhost:9200)** is the most active stack.`;

        const result = addAssistantResourceLinks(content, [
            toolResult([{ id: 'stack-1', title: 'Connection refused (localhost:9200)', webUrl: '/stack/stack-1' }])
        ]);

        expect(result).toContain('| Error | [Connection refused (localhost:9200)](/stack/stack-1) |');
        expect(result).toContain('**[Connection refused (localhost:9200)](/stack/stack-1)**');
    });

    it('preserves existing links and code examples', () => {
        const content = `[Timeout expired](/stack/existing) is already linked.

\`Timeout expired\`

\`\`\`text
Timeout expired
\`\`\``;

        expect(addAssistantResourceLinks(content, [toolResult([{ title: 'Timeout expired', webUrl: '/stack/stack-1' }])])).toBe(content);
    });

    it('preserves complete inline-link destinations with balanced parentheses', () => {
        const content = '[docs](https://example.test/(guide)/API)';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(content);
    });

    it('preserves resource labels inside indented code blocks', () => {
        const content = `    API.connect()

Use API.`;

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(`    API.connect()

Use [API](/project/api).`);
    });

    it('preserves indented code nested inside blockquotes', () => {
        const content = `>     API.connect()

Use API.`;

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(`>     API.connect()

Use [API](/project/api).`);
    });

    it('preserves multi-backtick code spans containing shorter backtick runs', () => {
        const content = '``code ` API`` and API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe('``code ` API`` and [API](/project/api).');
    });

    it('preserves fenced code when the delimiter occurs mid-line', () => {
        const content = ['```ts', 'const marker = "```";', 'API.connect()', '```', '', 'Use API.'].join('\n');
        const expected = ['```ts', 'const marker = "```";', 'API.connect()', '```', '', 'Use [API](/project/api).'].join('\n');

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(expected);
    });

    it('preserves fenced code nested inside blockquotes', () => {
        const content = ['> ```ts', '> API.connect()', '> ```', '', 'Use API.'].join('\n');
        const expected = ['> ```ts', '> API.connect()', '> ```', '', 'Use [API](/project/api).'].join('\n');

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(expected);
    });

    it('preserves tilde-fenced code nested inside lists', () => {
        const content = ['- ~~~ts', '  API.connect()', '  ~~~', '', 'Use API.'].join('\n');
        const expected = ['- ~~~ts', '  API.connect()', '  ~~~', '', 'Use [API](/project/api).'].join('\n');

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(expected);
    });

    it('preserves resource labels inside email addresses', () => {
        const content = 'Contact API@example.com before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(
            'Contact API@example.com before opening [API](/project/api).'
        );
    });

    it('preserves resource labels inside bare hostnames', () => {
        const content = 'Connect to API.example.com before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(
            'Connect to API.example.com before opening [API](/project/api).'
        );
    });

    it('preserves reference-style and shortcut links', () => {
        const content = `[Timeout expired][stack], [Timeout expired][], and [Timeout expired] are already linked.

[stack]: /stack/existing`;

        expect(addAssistantResourceLinks(content, [toolResult([{ title: 'Timeout expired', webUrl: '/stack/stack-1' }])])).toBe(content);
    });

    it('links only complete resource labels', () => {
        const content = 'APIClient uses the API project.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(
            'APIClient uses the [API](/project/api) project.'
        );
    });

    it('preserves labels inside bare absolute and relative URLs', () => {
        const content = 'See https://example.test/API and /project/API before opening API.';

        expect(addAssistantResourceLinks(content, [toolResult([{ name: 'API', webUrl: '/project/api' }])])).toBe(
            'See https://example.test/API and /project/API before opening [API](/project/api).'
        );
    });

    it('ignores punctuation-only resource labels', () => {
        const content = `- Keep this list item

| Name | Count |
| --- | --- |`;

        expect(addAssistantResourceLinks(content, [toolResult([{ name: '-', webUrl: '/project/dash' }])])).toBe(content);
    });

    it('does not guess when duplicate titles refer to different resources', () => {
        const content = 'Investigate Timeout expired.';
        const tools = [
            toolResult([
                { title: 'Timeout expired', webUrl: '/stack/stack-1' },
                { title: 'Timeout expired', webUrl: '/stack/stack-2' }
            ])
        ];

        expect(addAssistantResourceLinks(content, tools)).toBe(content);
    });

    it('ignores nested and external URLs from untrusted result data', () => {
        const content = 'Do not link this title.';
        const tools = [
            toolResult([
                {
                    data: { title: 'this title', webUrl: '/stack/untrusted' },
                    title: 'External title',
                    webUrl: 'https://example.com/stack/1'
                }
            ])
        ];

        expect(addAssistantResourceLinks(content, tools)).toBe(content);
    });
});
