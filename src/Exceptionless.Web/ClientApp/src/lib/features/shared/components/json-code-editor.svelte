<script lang="ts">
    import type { HTMLTextareaAttributes } from 'svelte/elements';

    import { CodeBlock } from '$comp/typography';
    import { Textarea } from '$comp/ui/textarea';

    type Props = Omit<HTMLTextareaAttributes, 'class' | 'value'> & {
        class?: string;
        value?: string;
    };

    let { class: className, value = $bindable(''), ...restProps }: Props = $props();
    let scrollLeft = $state(0);
    let scrollTop = $state(0);

    const highlightStyle = $derived(`position: absolute; top: 0; left: 0; transform: translate(${-scrollLeft}px, ${-scrollTop}px);`);

    function handleScroll(event: Event) {
        const target = event.currentTarget as HTMLTextAreaElement;
        scrollLeft = target.scrollLeft;
        scrollTop = target.scrollTop;
    }
</script>

<div class={['border-input bg-muted relative h-96 max-h-[60vh] min-h-96 overflow-hidden rounded border', className]}>
    <CodeBlock
        code={value || ' '}
        language="json"
        aria-hidden="true"
        class="pointer-events-none m-0 min-w-full rounded-none px-[0.6rem] py-2 text-xs leading-normal"
        style={highlightStyle}
    />
    <Textarea
        class="caret-foreground selection:bg-primary/30 relative z-10 block h-full min-h-0 w-full resize-none overflow-auto rounded-none border-0 bg-transparent px-[0.6rem] py-2 font-mono text-xs leading-normal text-transparent outline-none selection:text-transparent focus-visible:border-0 focus-visible:ring-0"
        bind:value
        wrap="off"
        spellcheck={false}
        onscroll={handleScroll}
        {...restProps}
    />
</div>
