import type { AssistantToolActivity } from './models';

interface AssistantResourceLink {
    label: string;
    url: string;
}

export function addAssistantResourceLinks(content: string, tools: AssistantToolActivity[]): string {
    const links = getAssistantResourceLinks(tools);
    if (links.length === 0) {
        return content;
    }

    return replaceOutsideProtectedMarkdown(content, (text) => {
        const linksByLabel = new Map(links.map((link) => [link.label, link]));
        const linkPattern = new RegExp(
            `(?<![\\p{L}\\p{N}_])(?:${links.map((link) => escapeRegularExpression(link.label)).join('|')})(?![\\p{L}\\p{N}_])`,
            'gu'
        );

        return text.replace(linkPattern, (label) => {
            const link = linksByLabel.get(label);
            return link ? `[${escapeMarkdownLabel(link.label)}](${link.url})` : label;
        });
    });
}

export function normalizeAssistantUrl(url: string, key: string): string {
    if (key !== 'href') {
        return url;
    }

    try {
        const parsedUrl = new URL(url);
        if (parsedUrl.pathname === '/next' || parsedUrl.pathname.startsWith('/next/')) {
            return `${parsedUrl.pathname}${parsedUrl.search}${parsedUrl.hash}`;
        }
    } catch {
        // Relative URLs are already same-origin and should remain unchanged.
    }

    return url;
}

function collectAssistantResourceLink(value: unknown, urlsByLabel: Map<string, Set<string>>): void {
    if (!isRecord(value)) {
        return;
    }

    const label = readNonEmptyString(value, 'title') ?? readNonEmptyString(value, 'name');
    const url = readNonEmptyString(value, 'webUrl');
    if (label && url && (url === '/next' || url.startsWith('/next/'))) {
        const urls = urlsByLabel.get(label) ?? new Set<string>();
        urls.add(url);
        urlsByLabel.set(label, urls);
    }
}

function collectAssistantResourceLinks(value: unknown, urlsByLabel: Map<string, Set<string>>): void {
    if (!isRecord(value) || !isRecord(value.data)) {
        return;
    }

    if (Array.isArray(value.data.items)) {
        for (const item of value.data.items) {
            collectAssistantResourceLink(item, urlsByLabel);
        }
    } else {
        collectAssistantResourceLink(value.data, urlsByLabel);
    }
}

function countRun(content: string, start: number, character: string): number {
    let end = start;
    while (content[end] === character) {
        end++;
    }

    return end - start;
}

function escapeMarkdownLabel(value: string): string {
    return value.replaceAll('\\', '\\\\').replaceAll('[', '\\[').replaceAll(']', '\\]');
}

function escapeRegularExpression(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function findFencedCodeRanges(content: string): Array<{ end: number; start: number }> {
    const ranges: Array<{ end: number; start: number }> = [];
    const openingFence =
        /^(?<blockquote>(?: {0,3}>[\t ]?)*)(?:(?<list> {0,3}(?:[-+*]|\d{1,9}[.)])[\t ]+)| {0,3})(?<delimiter>`{3,}|~{3,})[^\r\n]*(?:\r?\n|$)/gm;
    let openingMatch: null | RegExpExecArray;

    while ((openingMatch = openingFence.exec(content)) !== null) {
        const delimiter = openingMatch.groups?.delimiter;
        if (!delimiter) {
            continue;
        }

        const blockquoteDepth = [...(openingMatch.groups?.blockquote ?? '')].filter((character) => character === '>').length;
        const blockquotePrefix = blockquoteDepth > 0 ? `(?: {0,3}>[\\t ]?){${blockquoteDepth}}` : '';
        const listIndent = openingMatch.groups?.list?.length ?? 0;
        const containerIndent = listIndent > 0 ? `[\\t ]{${listIndent},${listIndent + 3}}` : ' {0,3}';
        const closingFence = new RegExp(
            `^${blockquotePrefix}${containerIndent}${escapeRegularExpression(delimiter.charAt(0))}{${delimiter.length},}[\\t ]*(?:\\r?$)`,
            'gm'
        );
        closingFence.lastIndex = openingFence.lastIndex;
        const closingMatch = closingFence.exec(content);
        const end = closingMatch ? closingMatch.index + closingMatch[0].length : content.length;
        ranges.push({ end, start: openingMatch.index });
        openingFence.lastIndex = end;
    }

    return ranges;
}

function findInlineCodeRanges(content: string): Array<{ end: number; start: number }> {
    const ranges: Array<{ end: number; start: number }> = [];

    for (let index = 0; index < content.length; index++) {
        if (content[index] !== '`') {
            continue;
        }

        const delimiterLength = countRun(content, index, '`');
        let cursor = index + delimiterLength;
        while (cursor < content.length) {
            if (content[cursor] !== '`') {
                cursor++;
                continue;
            }

            const candidateLength = countRun(content, cursor, '`');
            if (candidateLength === delimiterLength) {
                const end = cursor + candidateLength;
                ranges.push({ end, start: index });
                index = end - 1;
                break;
            }

            cursor += candidateLength;
        }
    }

    return ranges;
}

function findInlineLinkRanges(content: string): Array<{ end: number; start: number }> {
    const ranges: Array<{ end: number; start: number }> = [];

    for (let index = 0; index < content.length; index++) {
        const start = content[index] === '!' && content[index + 1] === '[' ? index : content[index] === '[' ? index : -1;
        if (start < 0) {
            continue;
        }

        let cursor = start + (content[start] === '!' ? 2 : 1);
        let labelDepth = 1;
        for (; cursor < content.length && labelDepth > 0; cursor++) {
            if (content[cursor] === '\n' || content[cursor] === '\r') {
                break;
            }

            if (content[cursor] === '\\') {
                cursor++;
            } else if (content[cursor] === '[') {
                labelDepth++;
            } else if (content[cursor] === ']') {
                labelDepth--;
            }
        }

        if (labelDepth !== 0 || content[cursor] !== '(') {
            continue;
        }

        let destinationDepth = 1;
        cursor++;
        for (; cursor < content.length && destinationDepth > 0; cursor++) {
            if (content[cursor] === '\n' || content[cursor] === '\r') {
                break;
            }

            if (content[cursor] === '\\') {
                cursor++;
            } else if (content[cursor] === '(') {
                destinationDepth++;
            } else if (content[cursor] === ')') {
                destinationDepth--;
            }
        }

        if (destinationDepth === 0) {
            ranges.push({ end: cursor, start });
            index = cursor - 1;
        }
    }

    return ranges;
}

function getAssistantResourceLinks(tools: AssistantToolActivity[]): AssistantResourceLink[] {
    const urlsByLabel = new Map<string, Set<string>>();

    for (const tool of tools) {
        if (tool.status !== 'complete' || !tool.result) {
            continue;
        }

        try {
            collectAssistantResourceLinks(JSON.parse(tool.result) as unknown, urlsByLabel);
        } catch {
            // Tool activity can contain non-JSON diagnostic text.
        }
    }

    return [...urlsByLabel]
        .filter(([label, urls]) => /[\p{L}\p{N}]/u.test(label) && urls.size === 1)
        .flatMap(([label, urls]) => {
            const url = urls.values().next().value;
            return url ? [{ label, url }] : [];
        })
        .sort((left, right) => right.label.length - left.label.length);
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
}

function readNonEmptyString(record: Record<string, unknown>, key: string): string | undefined {
    const value = record[key];
    return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function replaceOutsideProtectedMarkdown(content: string, replace: (text: string) => string): string {
    const protectedMarkdown =
        /(^(?:(?: {4}|\t)[^\r\n]*(?:\r?\n|$))+|!?\[[^\]\n]*\]\s*\[[^\]\n]*\]|!?\[[^\]\n]*\]|[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*|https?:\/\/[^\s<]+|\/next(?:\/[^\s<]*)?)/gm;
    const protectedRanges = [...content.matchAll(protectedMarkdown)].map((match) => ({
        end: match.index + match[0].length,
        start: match.index
    }));
    protectedRanges.push(...findFencedCodeRanges(content));
    protectedRanges.push(...findInlineCodeRanges(content));
    protectedRanges.push(...findInlineLinkRanges(content));
    protectedRanges.sort((left, right) => left.start - right.start || left.end - right.end);

    let result = '';
    let previousIndex = 0;

    for (const range of protectedRanges) {
        if (range.end <= previousIndex) {
            continue;
        }

        if (range.start > previousIndex) {
            result += replace(content.slice(previousIndex, range.start));
        }

        const protectedStart = Math.max(previousIndex, range.start);
        result += content.slice(protectedStart, range.end);
        previousIndex = range.end;
    }

    return result + replace(content.slice(previousIndex));
}
