<script lang="ts">
    import { cn } from '$lib/utils';
    // Add plugins as needed
    // pnpm add @streamdown-svelte/code @streamdown-svelte/mermaid @streamdown-svelte/math @streamdown-svelte/cjk
    // import { code } from '@streamdown-svelte/code';
    // import { mermaid } from '@streamdown-svelte/mermaid';
    // import { math } from '@streamdown-svelte/math';
    // import { cjk } from '@streamdown-svelte/cjk';
    // import 'katex/dist/katex.min.css';
    import githubDarkDefault from '@shikijs/themes/github-dark-default';
    import githubLightDefault from '@shikijs/themes/github-light-default';
    import { mode } from 'mode-watcher';
    import { Streamdown, type StreamdownProps } from 'streamdown-svelte';
    type Props = StreamdownProps;

    let { class: className, components, content, ...restProps }: Props = $props();
    let currentTheme = $derived(mode.current === 'dark' ? 'github-dark-default' : 'github-light-default');

    const assistantTheme = {
        blockquote: { base: 'border-muted-foreground/30 text-muted-foreground my-3 border-l-3 pl-3' },
        code: {
            actions: 'pointer-events-none sticky top-2 z-10 -mt-9 flex h-7 items-center justify-end',
            base: 'my-3 flex w-full flex-col gap-1.5 rounded-lg border border-border bg-sidebar p-1.5',
            buttons:
                'pointer-events-auto flex shrink-0 items-center gap-1 rounded-md border border-sidebar bg-sidebar/90 px-1 py-0.5 supports-[backdrop-filter]:bg-sidebar/75 supports-[backdrop-filter]:backdrop-blur',
            container: 'overflow-x-auto rounded-md border border-border bg-background p-3 text-xs',
            header: 'flex h-7 items-center justify-between text-muted-foreground text-xs'
        },
        components: {
            button: 'disabled:cursor-not-allowed disabled:opacity-50 cursor-pointer p-1 text-muted-foreground transition-colors hover:text-foreground rounded hover:bg-border flex items-center justify-center size-7'
        },
        h1: { base: 'mt-5 mb-2 text-xl font-semibold text-foreground' },
        h2: { base: 'mt-5 mb-2 text-lg font-semibold text-foreground' },
        h3: { base: 'mt-4 mb-1.5 text-base font-semibold text-foreground' },
        h4: { base: 'mt-3 mb-1 text-sm font-semibold text-foreground' },
        li: { base: 'py-0.5' },
        ol: { base: 'my-2 ml-5 list-outside list-decimal whitespace-normal text-foreground' },
        paragraph: { base: 'my-2 leading-relaxed text-foreground' },
        table: {
            container: 'max-w-full overflow-x-auto rounded-lg border border-border bg-background',
            table: 'w-full min-w-[28rem] border-collapse',
            toolbar:
                'ml-auto flex w-fit items-center justify-end gap-0.5 rounded-md border border-sidebar bg-sidebar/90 p-0.5 supports-[backdrop-filter]:bg-sidebar/75 supports-[backdrop-filter]:backdrop-blur',
            wrapper: 'my-3 flex flex-col gap-1.5 rounded-xl border border-border bg-sidebar p-1.5'
        },
        td: {
            base: 'min-w-0 max-w-none px-3 py-2.5 align-top text-sm text-foreground first:min-w-56 [&:not(:first-child)]:w-px [&:not(:first-child)]:whitespace-nowrap'
        },
        th: {
            base: 'min-w-0 max-w-none px-3 py-2 text-left align-bottom text-xs font-semibold text-foreground first:min-w-56 [&:not(:first-child)]:w-px [&:not(:first-child)]:whitespace-nowrap'
        },
        ul: { base: 'my-2 ml-5 list-outside list-disc whitespace-normal text-foreground' }
    } satisfies NonNullable<StreamdownProps['theme']>;
</script>

<div class={cn('w-full min-w-0 [&>*:first-child]:mt-0 [&>*:last-child]:mb-0', className)}>
    <Streamdown
        {content}
        {components}
        baseTheme="shadcn"
        controls={{ code: { copy: true, download: false }, table: { copy: true, download: false, fullscreen: true } }}
        mergeTheme={true}
        shikiTheme={currentTheme}
        shikiThemes={{
            'github-dark-default': githubDarkDefault,
            'github-light-default': githubLightDefault
        }}
        theme={assistantTheme}
        // plugins={{ code, mermaid, math, cjk }}
        {...restProps}
    />
</div>
