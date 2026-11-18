<script lang="ts">
    import ArrowUp from '@lucide/svelte/icons/arrow-up';
    import Calendar from '@lucide/svelte/icons/calendar';
    import Folder from '@lucide/svelte/icons/folder';
    import Layers from '@lucide/svelte/icons/layers';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Square from '@lucide/svelte/icons/square';
    import X from '@lucide/svelte/icons/x';

    import * as InputGroup from '#comp/ui/input-group/index.ts';
    import * as Kbd from '#comp/ui/kbd/index.ts';

    import type { AssistantContext } from '../assistant-starters';
    import type { AssistantProject } from '../models';

    import AssistantProjectScope from './assistant-project-scope.svelte';

    interface Props {
        context?: AssistantContext;
        contextDismissed?: boolean;
        isEditing?: boolean;
        isStreaming?: boolean;
        onCancelEdit?: () => void;
        onProjectChange?: (projectId: string | undefined) => void;
        onStop: () => void;
        onSubmit: (value: string) => void;
        /** The project Exie searches; undefined means all projects. */
        projectId?: string;
        /** Projects the user can scope Exie to. The picker is hidden when there is no choice to make. */
        projects?: AssistantProject[];
        value?: string;
    }

    let {
        context,
        contextDismissed = $bindable(false),
        isEditing = false,
        isStreaming = false,
        onCancelEdit,
        onProjectChange,
        onStop,
        onSubmit,
        projectId,
        projects = [],
        value = $bindable('')
    }: Props = $props();
    let textareaElement = $state<HTMLTextAreaElement | null>(null);
    let canSend = $derived(!isStreaming && value.trim().length > 0);
    let contextLabel = $derived(context?.kind === 'event' ? 'Event' : context?.kind === 'stack' ? 'Stack' : 'Project');
    let placeholder = $derived(
        isEditing ? 'Edit your message…' : context && !contextDismissed ? `Ask about this ${contextLabel.toLowerCase()}…` : 'Ask Exie anything…'
    );

    $effect(() => {
        void value;
        resizeTextarea();
    });

    /**
     * Moves focus to the message box. Automatic focus is skipped on touch screens, where it would
     * open the on-screen keyboard and make iOS pan the fixed panel out of view; pass `force` when
     * the user asked to type, such as editing a message.
     */
    export function focus(options: { force?: boolean } = {}): void {
        if (!options.force && !window.matchMedia('(pointer: fine)').matches) {
            return;
        }

        textareaElement?.focus();
    }

    function handleKeydown(event: KeyboardEvent): void {
        if (event.key === 'Escape' && isEditing) {
            event.preventDefault();
            event.stopPropagation();
            onCancelEdit?.();
            return;
        }

        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            submit();
        }
    }

    function resizeTextarea(): void {
        if (!textareaElement) {
            return;
        }

        textareaElement.style.height = 'auto';
        textareaElement.style.height = `${Math.min(textareaElement.scrollHeight, 200)}px`;
    }

    function submit(): void {
        if (canSend) {
            onSubmit(value);
        }
    }
</script>

{#snippet contextIcon()}
    {#if context?.kind === 'event'}
        <Calendar aria-hidden="true" class="size-3.5 shrink-0" />
    {:else if context?.kind === 'stack'}
        <Layers aria-hidden="true" class="size-3.5 shrink-0" />
    {:else}
        <Folder aria-hidden="true" class="size-3.5 shrink-0" />
    {/if}
{/snippet}

<form
    class="w-full"
    onsubmit={(event) => {
        event.preventDefault();
        submit();
    }}
>
    <InputGroup.Root class="bg-background h-auto rounded-xl shadow-xs">
        {#if isEditing || context}
            <InputGroup.Addon align="block-start" class="flex-wrap gap-1.5">
                {#if isEditing}
                    <span class="bg-primary/10 text-primary inline-flex h-6 items-center gap-1 rounded-md pr-0.5 pl-2 text-xs font-medium">
                        <Pencil aria-hidden="true" class="size-3" />
                        Editing message
                        <InputGroup.Button aria-label="Cancel editing" class="size-5" onclick={onCancelEdit} size="icon-xs" variant="ghost">
                            <X aria-hidden="true" />
                        </InputGroup.Button>
                    </span>
                {/if}
                {#if context && !contextDismissed}
                    <span
                        class="bg-muted text-foreground inline-flex h-6 max-w-full min-w-0 items-center gap-1 rounded-md pr-0.5 pl-2 text-xs font-medium"
                        data-testid="assistant-context-chip"
                        title={context.title ? `${contextLabel}: ${context.title}` : `This ${contextLabel.toLowerCase()}`}
                    >
                        {@render contextIcon()}
                        <span class="text-muted-foreground shrink-0">{contextLabel}:</span>
                        <span class="min-w-0 truncate">{context.title ?? `This ${contextLabel.toLowerCase()}`}</span>
                        <InputGroup.Button
                            aria-label={`Don’t use this ${contextLabel.toLowerCase()} as context`}
                            class="size-5"
                            onclick={() => (contextDismissed = true)}
                            size="icon-xs"
                            variant="ghost"
                        >
                            <X aria-hidden="true" />
                        </InputGroup.Button>
                    </span>
                {:else if context}
                    <InputGroup.Button class="h-6 text-xs" onclick={() => (contextDismissed = false)} size="xs" variant="ghost">
                        <Plus aria-hidden="true" />
                        Use this {contextLabel.toLowerCase()} as context
                    </InputGroup.Button>
                {/if}
            </InputGroup.Addon>
        {/if}
        <InputGroup.Textarea
            aria-describedby="assistant-composer-help"
            aria-label="Message Exie"
            bind:ref={textareaElement}
            bind:value
            class="max-h-50 min-h-11 px-3 pt-2.5 pb-1 text-sm"
            data-tour="exie-input"
            onkeydown={handleKeydown}
            {placeholder}
            rows={1}
        />
        <InputGroup.Addon align="block-end" class="justify-between gap-2 pt-0">
            {#if onProjectChange && projects.length > 1}
                <AssistantProjectScope disabled={isStreaming} onChange={onProjectChange} {projects} value={projectId} />
            {/if}
            <span
                class="text-muted-foreground hidden min-w-0 items-center gap-1 text-[11px] font-normal whitespace-nowrap pointer-fine:flex"
                id="assistant-composer-help"
            >
                <Kbd.Root>↵</Kbd.Root> to send <span aria-hidden="true">·</span>
                <Kbd.Root>⇧</Kbd.Root><Kbd.Root>↵</Kbd.Root> new line
            </span>
            {#if isStreaming}
                <InputGroup.Button aria-label="Stop generating" class="ml-auto rounded-full" onclick={onStop} size="icon-sm" variant="default">
                    <Square aria-hidden="true" class="size-3 fill-current" />
                </InputGroup.Button>
            {:else}
                <InputGroup.Button aria-label="Send message" class="ml-auto rounded-full" disabled={!canSend} size="icon-sm" type="submit" variant="default">
                    <ArrowUp aria-hidden="true" />
                </InputGroup.Button>
            {/if}
        </InputGroup.Addon>
    </InputGroup.Root>
</form>
