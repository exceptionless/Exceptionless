<script lang="ts">
    import Check from '@lucide/svelte/icons/check';
    import Copy from '@lucide/svelte/icons/copy';
    import Pencil from '@lucide/svelte/icons/pencil';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import ThumbsDown from '@lucide/svelte/icons/thumbs-down';
    import ThumbsUp from '@lucide/svelte/icons/thumbs-up';
    import { toast } from 'svelte-sonner';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Popover from '#comp/ui/popover/index.ts';
    import * as Tooltip from '#comp/ui/tooltip/index.ts';
    import { UseClipboard } from '#lib/hooks/use-clipboard.svelte.ts';

    import type { AssistantFeedback, AssistantFeedbackReason } from '../models';

    interface Props {
        align?: 'end' | 'start';
        content: string;
        feedback?: AssistantFeedback;
        feedbackReason?: AssistantFeedbackReason;
        onCopy?: () => void;
        onEdit?: () => void;
        onFeedback?: (feedback: AssistantFeedback | undefined, reason?: AssistantFeedbackReason) => void;
        onRegenerate?: () => Promise<void> | void;
        showFeedback?: boolean;
    }

    let { align = 'start', content, feedback, feedbackReason, onCopy, onEdit, onFeedback, onRegenerate, showFeedback = false }: Props = $props();
    const clipboard = new UseClipboard({
        delay: 1500
    });
    const feedbackReasons: Array<{ label: string; value: AssistantFeedbackReason }> = [
        {
            label: 'Incorrect',
            value: 'incorrect'
        },
        {
            label: 'Incomplete',
            value: 'incomplete'
        },
        {
            label: 'Not relevant',
            value: 'not-relevant'
        },
        {
            label: 'Wrong change',
            value: 'wrong-action'
        },
        {
            label: 'Something else',
            value: 'other'
        }
    ];
    let isRegenerating = $state(false);
    let isReasonOpen = $state(false);

    async function copyMessage(): Promise<void> {
        if ((await clipboard.copy(content)) === 'success') {
            onCopy?.();
        }
    }

    function markHelpful(): void {
        onFeedback?.(feedback === 'helpful' ? undefined : 'helpful');
    }

    async function regenerate(): Promise<void> {
        if (!onRegenerate || isRegenerating) {
            return;
        }

        isRegenerating = true;
        try {
            await onRegenerate();
        } catch {
            toast.error('Couldn’t regenerate the response. Please try again.');
        }
        isRegenerating = false;
    }

    function selectReason(reason: AssistantFeedbackReason): void {
        onFeedback?.('not-helpful', reason);
        isReasonOpen = false;
    }

    // The popover trigger handles both pointer and keyboard activation, so the rating follows its open state.
    function setReasonOpen(open: boolean): void {
        if (open && feedback === 'not-helpful') {
            isReasonOpen = false;
            onFeedback?.(undefined);
            return;
        }

        if (open) {
            onFeedback?.('not-helpful');
        }

        isReasonOpen = open;
    }
</script>

{#snippet action(label: string, onclick: () => void, icon: typeof Copy, options: { disabled?: boolean; pressed?: boolean } = {})}
    {@const Icon = icon}
    <Tooltip.Root>
        <Tooltip.Trigger>
            {#snippet child({ props })}
                <Button
                    {...props}
                    aria-label={label}
                    aria-pressed={options.pressed}
                    class="text-muted-foreground hover:text-foreground aria-pressed:text-foreground"
                    disabled={options.disabled}
                    {onclick}
                    size="icon-xs"
                    variant="ghost"
                >
                    <Icon aria-hidden="true" class={options.pressed ? 'fill-current' : undefined} />
                </Button>
            {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content>{label}</Tooltip.Content>
    </Tooltip.Root>
{/snippet}

<Tooltip.Provider delayDuration={300}>
    <div
        aria-label="Message actions"
        class={[
            'flex items-center gap-0.5 opacity-100 transition-opacity sm:opacity-0 sm:group-focus-within/message:opacity-100 sm:group-hover/message:opacity-100',
            align === 'end' && 'justify-end',
            (feedback || isReasonOpen) && 'sm:opacity-100'
        ]}
        role="toolbar"
    >
        {@render action(clipboard.copied ? 'Copied' : 'Copy message', () => void copyMessage(), clipboard.copied ? Check : Copy)}

        {#if onEdit}
            {@render action('Edit and resend', onEdit, Pencil)}
        {/if}

        {#if onRegenerate}
            {@render action(isRegenerating ? 'Regenerating response' : 'Regenerate response', () => void regenerate(), RotateCcw, {
                disabled: isRegenerating
            })}
        {/if}

        {#if showFeedback}
            {@render action('Good response', markHelpful, ThumbsUp, {
                pressed: feedback === 'helpful'
            })}
            <Popover.Root bind:open={() => isReasonOpen, setReasonOpen}>
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props: tooltipProps })}
                            <Popover.Trigger {...tooltipProps}>
                                {#snippet child({ props })}
                                    <Button
                                        {...props}
                                        aria-label="Poor response"
                                        aria-pressed={feedback === 'not-helpful'}
                                        class="text-muted-foreground hover:text-foreground aria-pressed:text-foreground"
                                        size="icon-xs"
                                        variant="ghost"
                                    >
                                        <ThumbsDown aria-hidden="true" class={feedback === 'not-helpful' ? 'fill-current' : undefined} />
                                    </Button>
                                {/snippet}
                            </Popover.Trigger>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>Poor response</Tooltip.Content>
                </Tooltip.Root>
                <Popover.Content align="start" class="flex w-64 flex-col gap-2 p-3 text-sm" side="top">
                    <p class="font-medium">What went wrong?</p>
                    <div class="flex flex-wrap gap-1.5">
                        {#each feedbackReasons as reason (reason.value)}
                            <Button
                                aria-pressed={feedbackReason === reason.value}
                                onclick={() => selectReason(reason.value)}
                                size="xs"
                                variant={feedbackReason === reason.value ? 'secondary' : 'outline'}
                            >
                                {reason.label}
                            </Button>
                        {/each}
                    </div>
                </Popover.Content>
            </Popover.Root>
        {/if}
    </div>
</Tooltip.Provider>
