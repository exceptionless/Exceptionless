<script lang="ts">
    // NOTE: We are disabling html tags warnings as we are running it through DOMPurify.
    /* eslint svelte/no-at-html-tags: "off" */
    import csharpLanguage from '@shikijs/langs/csharp';
    import javaScriptLanguage from '@shikijs/langs/javascript';
    import jsonLanguage from '@shikijs/langs/json';
    import powershellLanguage from '@shikijs/langs/powershell';
    import shellScriptLanguage from '@shikijs/langs/shellscript';
    import xmlLanguage from '@shikijs/langs/xml';
    import githubDark from '@shikijs/themes/github-dark';
    import githubLight from '@shikijs/themes/github-light';
    import DOMPurify from 'dompurify';
    import { mode } from 'mode-watcher';
    import { createHighlighterCoreSync } from 'shiki/core';
    import { createJavaScriptRegexEngine } from 'shiki/engine/javascript';

    interface Props {
        code: string;
        language: 'csharp' | 'javascript' | 'json' | 'powershell' | 'shellscript' | 'xml';
    }

    const { code, language }: Props = $props();

    let theme = $derived($mode === 'light' ? 'github-light' : 'github-dark');
    const jsEngine = createJavaScriptRegexEngine();

    const highlighter = createHighlighterCoreSync({
        engine: jsEngine,
        langs: [csharpLanguage, javaScriptLanguage, jsonLanguage, powershellLanguage, shellScriptLanguage, xmlLanguage],
        themes: [githubDark, githubLight]
    });

    const purify = DOMPurify(window);
    const content = $derived(
        purify.sanitize(
            highlighter.codeToHtml(code, {
                colorReplacements: {
                    'github-dark': {
                        '#24292e': 'inherit',
                        '#e1e4e8': 'inherit'
                    },
                    'github-light': {
                        '#24292e': 'inherit',
                        '#defdef': 'inherit',
                        '#fff': 'inherit'
                    }
                },
                lang: language,
                theme: theme
            })
        )
    );
</script>

<pre class="bg-muted relative rounded px-[0.3rem] py-[0.2rem] font-mono text-sm">{@html content}</pre>
