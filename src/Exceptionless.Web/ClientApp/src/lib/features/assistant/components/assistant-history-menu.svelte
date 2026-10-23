<script lang="ts">
    import History from '@lucide/svelte/icons/history';
    import Trash2 from '@lucide/svelte/icons/trash-2';

    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Popover from '#comp/ui/popover/index.ts';
    import * as Tooltip from '#comp/ui/tooltip/index.ts';

    import type { AssistantConversation } from '../models';

    interface Props {
        activeId?: string;
        conversations: AssistantConversation[];
        disabled?: boolean;
        onDelete: (id: string) => void;
        onSelect: (id: string) => void;
    }

    let { activeId, conversations, disabled = false, onDelete, onSelect }: Props = $props();
    let open = $state(false);

    function select(id: string): void {
        open = false;
        onSelect(id);
    }
</script>

<Popover.Root bind:open>
    <Tooltip.Provider delayDuration={300}>
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props: tooltipProps })}
                    <Popover.Trigger {...tooltipProps} {disabled}>
                        {#snippet child({ props })}
                            <Button {...props} aria-label="Recent chats" size="icon-sm" variant="ghost">
                                <History aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Popover.Trigger>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>Recent chats</Tooltip.Content>
        </Tooltip.Root>
    </Tooltip.Provider>
    <Popover.Content align="end" aria-label="Recent chats" class="flex w-80 flex-col gap-1 p-1.5">
        <p class="text-muted-foreground px-2 pt-1 pb-1.5 text-xs font-medium">Recent chats</p>
        {#if conversations.length === 0}
            <p class="text-muted-foreground px-2 pb-2 text-sm">Your chats with Exie will appear here.</p>
        {:else}
            <ul class="flex max-h-80 flex-col gap-0.5 overflow-y-auto">
                {#each conversations as conversation (conversation.id)}
                    <li class="group/history hover:bg-muted has-[[aria-current=true]]:bg-muted flex items-center gap-1 rounded-md">
                        <button
                            aria-current={conversation.id === activeId}
                            class="focus-visible:ring-ring/50 flex min-w-0 flex-1 flex-col items-start rounded-md px-2 py-1.5 text-left outline-none focus-visible:ring-3"
                            onclick={() => select(conversation.id)}
                            type="button"
                        >
                            <span class="w-full truncate text-sm">{conversation.title}</span>
                            <span class="text-muted-foreground text-xs"><TimeAgo value={new Date(conversation.updatedAt)} /></span>
                        </button>
                        <Button
                            aria-label={`Delete “${conversation.title}”`}
                            class="text-muted-foreground hover:text-destructive mr-1 opacity-100 sm:opacity-0 sm:group-focus-within/history:opacity-100 sm:group-hover/history:opacity-100"
                            onclick={() => onDelete(conversation.id)}
                            size="icon-xs"
                            variant="ghost"
                        >
                            <Trash2 aria-hidden="true" />
                        </Button>
                    </li>
                {/each}
            </ul>
        {/if}
    </Popover.Content>
</Popover.Root>
