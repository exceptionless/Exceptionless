<script lang="ts">
    import * as Command from '$comp/ui/command';
    import * as InputGroup from '$comp/ui/input-group';
    import CircleStop from '@lucide/svelte/icons/circle-stop';
    import Send from '@lucide/svelte/icons/send';
    import Wrench from '@lucide/svelte/icons/wrench';

    interface Props {
        isStreaming?: boolean;
        onStop: () => void;
        onSubmit: (value: string) => void;
        showToolCalls?: boolean;
        value?: string;
    }

    let { isStreaming = false, onStop, onSubmit, showToolCalls = false, value = $bindable('') }: Props = $props();
    let textareaElement = $state<HTMLTextAreaElement | null>(null);
    let slashCommandQuery = $derived(value.trim().toLowerCase());
    let showSlashCommands = $derived(!isStreaming && slashCommandQuery.startsWith('/') && '/tools'.startsWith(slashCommandQuery));

    $effect(() => {
        void value;
        resizeTextarea();
    });

    function handleKeydown(event: KeyboardEvent): void {
        if (event.key === 'Escape' && showSlashCommands) {
            event.preventDefault();
            value = '';
            return;
        }

        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            if (showSlashCommands) {
                selectToolsCommand();
                return;
            }

            if (!isStreaming && value.trim()) {
                onSubmit(value);
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

    function selectToolsCommand(): void {
        onSubmit('/tools');
    }
</script>

<div class="relative">
    {#if showSlashCommands}
        <Command.Root aria-label="Exie commands" class="absolute inset-x-0 bottom-full mb-2 h-auto shadow-md">
            <Command.List>
                <Command.Group heading="Commands">
                    <Command.Item onSelect={selectToolsCommand} value="/tools">
                        <Wrench aria-hidden="true" />
                        <div class="flex min-w-0 flex-col">
                            <code>/tools</code>
                            <span class="text-muted-foreground text-xs">{showToolCalls ? 'Hide' : 'Show'} tool calls in the conversation</span>
                        </div>
                    </Command.Item>
                </Command.Group>
            </Command.List>
        </Command.Root>
    {/if}

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
            <span class="text-muted-foreground text-[11px]" id="assistant-composer-help"> Enter to send · Shift+Enter for a new line · / for commands </span>
            {#if isStreaming}
                <InputGroup.Button aria-label="Stop generating" onclick={onStop} size="icon-sm" variant="outline">
                    <CircleStop aria-hidden="true" />
                </InputGroup.Button>
            {:else}
                <InputGroup.Button aria-label="Send message" disabled={!value.trim()} onclick={() => onSubmit(value)} size="icon-sm" variant="default">
                    <Send aria-hidden="true" />
                </InputGroup.Button>
            {/if}
        </InputGroup.Addon>
    </InputGroup.Root>
</div>
