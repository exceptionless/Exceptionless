<script lang="ts">
    import * as CodeBlock from '#comp/ui/code-block/index.ts';
    import { useHighlightedCode } from '#lib/hooks/use-highlighted-code.svelte.ts';

    import { formatAssistantToolJson } from '../assistant-tool-result';

    interface Props {
        title: string;
        value: string;
    }

    let { title, value }: Props = $props();
    let formatted = $derived(formatAssistantToolJson(value));
    const lines = useHighlightedCode(
        () => formatted,
        () => 'json'
    );
</script>

<CodeBlock.Root aria-label={title} size="sm">
    <CodeBlock.Header>
        <CodeBlock.Title>{title}</CodeBlock.Title>
        <CodeBlock.Actions>
            <CodeBlock.CopyButton aria-label={`Copy ${title.toLowerCase()}`} {value} />
        </CodeBlock.Actions>
    </CodeBlock.Header>
    <CodeBlock.Content class="max-h-64" wrap>
        {#each lines.current as line, index (index)}
            <CodeBlock.Line code={line} />
        {/each}
    </CodeBlock.Content>
</CodeBlock.Root>
