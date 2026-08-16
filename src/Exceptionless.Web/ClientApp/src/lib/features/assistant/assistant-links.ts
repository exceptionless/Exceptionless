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

function escapeMarkdownLabel(value: string): string {
    return value.replaceAll('\\', '\\\\').replaceAll('[', '\\[').replaceAll(']', '\\]');
}

function escapeRegularExpression(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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
        /(```[^\n]*\n[\s\S]*?```|~~~[^\n]*\n[\s\S]*?~~~|`+[^`\n]*`+|!?\[[^\]\n]*\]\([^\n)]*\)|!?\[[^\]\n]*\]\s*\[[^\]\n]*\]|!?\[[^\]\n]*\]|https?:\/\/[^\s<]+|\/next(?:\/[^\s<]*)?)/g;
    let result = '';
    let previousIndex = 0;

    for (const match of content.matchAll(protectedMarkdown)) {
        const matchIndex = match.index;
        result += replace(content.slice(previousIndex, matchIndex));
        result += match[0];
        previousIndex = matchIndex + match[0].length;
    }

    return result + replace(content.slice(previousIndex));
}
