import type { GrammarState } from 'shiki/core';

import { type HighlightedLine, type Highlighter, loadHighlighter } from '#lib/highlighter.ts';

/**
 * Highlights `code` in `language`. `current` holds one entry per line: a plain
 * string until the highlighter loads, then the line's tokens. Pass each one to
 * the `code` prop of a `CodeBlock.Line`.
 *
 * ```svelte
 * <script lang="ts">
 * 	const lines = useHighlightedCode(() => code, () => "json");
 * </script>
 *
 * {#each lines.current as line, index (index)}
 * 	<CodeBlock.Line code={line} />
 * {/each}
 * ```
 */
export function useHighlightedCode(value: () => string, language: () => string) {
    let loaded = $state.raw<null | {
        highlighter: Highlighter | null;
        language: string;
    }>(null);

    $effect(() => {
        const lang = language();
        let cancelled = false;

        loadHighlighter(lang)
            .then((next) => {
                if (!cancelled) {
                    loaded = {
                        highlighter: next,
                        language: lang
                    };
                }
            })
            .catch(() => {});

        return () => {
            cancelled = true;
        };
    });

    // The complete lines, highlighted. While the code streams, only the new
    // lines are tokenized, starting from the grammar state of the last one.
    let done: {
        highlighter: Highlighter | null;
        lines: HighlightedLine[];
        state?: GrammarState;
        text: string;
    } = {
        highlighter: null,
        lines: [],
        text: ''
    };

    const lines = $derived.by((): HighlightedLine[] => {
        const raw = value();
        // A trailing newline would render as an extra empty line.
        const code = raw.endsWith('\n') ? raw.slice(0, -1) : raw;
        const lang = language();
        const highlighter = loaded?.language === lang ? loaded.highlighter : null;

        if (!highlighter) {
            return code.split('\n');
        }

        let next = done;

        if (next.highlighter !== highlighter || !code.startsWith(next.text)) {
            next = {
                highlighter,
                lines: [],
                text: ''
            };
        }

        const end = code.lastIndexOf('\n') + 1;

        if (end > next.text.length) {
            const result = highlighter.highlight(code.slice(next.text.length, end - 1), next.state);

            next = {
                highlighter,
                lines: [...next.lines, ...result.lines],
                state: result.state,
                text: code.slice(0, end)
            };
        }

        done = next;

        // The last line is still being written, so it is tokenized every time.
        const tail = code.slice(end);

        return [...next.lines, highlighter.highlight(tail, next.state).lines[0] ?? tail];
    });

    return {
        get current() {
            return lines;
        }
    };
}
