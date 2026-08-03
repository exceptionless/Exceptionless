<script lang="ts">
    import * as InputGroup from '$comp/ui/input-group';
    import CircleStop from '@lucide/svelte/icons/circle-stop';
    import Send from '@lucide/svelte/icons/send';

    interface Props {
        isStreaming?: boolean;
        onStop: () => void;
        onSubmit: () => void;
        value?: string;
    }

    let { isStreaming = false, onStop, onSubmit, value = $bindable('') }: Props = $props();
    let textareaElement = $state<HTMLTextAreaElement | null>(null);

    $effect(() => {
        void value;
        resizeTextarea();
    });

    function handleKeydown(event: KeyboardEvent): void {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            if (!isStreaming && value.trim()) {
                onSubmit();
            }
        }
    }

    function resizeTextarea(): void {
        if (!textareaElement) {
            return;
        }

        textareaElement.style.height = 'auto';
        textareaElement.style.height = `${Math.min(textareaElement.scrollHeight, 160)}px`;
    }
</script>

<InputGroup.Root class="bg-background h-auto rounded-xl shadow-xs" data-disabled={false}>
    <InputGroup.Textarea
        aria-describedby="assistant-composer-help"
        aria-label="Message Exie"
        bind:ref={textareaElement}
        bind:value
        class="placeholder:text-muted-foreground/50 max-h-40 min-h-18 px-3 pt-3 text-sm"
        oninput={resizeTextarea}
        onkeydown={handleKeydown}
        placeholder="Ask Exie…"
        rows={1}
    />
    <InputGroup.Addon align="block-end" class="justify-between gap-2 px-2.5 pb-2">
        <span class="text-muted-foreground text-[11px]" id="assistant-composer-help">Enter to send · Shift+Enter for a new line</span>
        {#if isStreaming}
            <InputGroup.Button aria-label="Stop generating" onclick={onStop} size="icon-sm" variant="outline">
                <CircleStop aria-hidden="true" />
            </InputGroup.Button>
        {:else}
            <InputGroup.Button aria-label="Send message" disabled={!value.trim()} onclick={onSubmit} size="icon-sm" variant="default">
                <Send aria-hidden="true" />
            </InputGroup.Button>
        {/if}
    </InputGroup.Addon>
</InputGroup.Root>
