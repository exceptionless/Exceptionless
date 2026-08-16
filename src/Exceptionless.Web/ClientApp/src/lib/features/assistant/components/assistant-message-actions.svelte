<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Tooltip from '$comp/ui/tooltip';
    import { submitFeatureUsage } from '$features/auth/exceptionless-session';
    import { UseClipboard } from '$lib/hooks/use-clipboard.svelte';
    import Check from '@lucide/svelte/icons/check';
    import Clipboard from '@lucide/svelte/icons/clipboard';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import ThumbsDown from '@lucide/svelte/icons/thumbs-down';
    import ThumbsUp from '@lucide/svelte/icons/thumbs-up';

    import type { AssistantFeedback } from '../models';

    interface Props {
        align?: 'end' | 'start';
        content: string;
        feedback?: AssistantFeedback;
        onFeedback?: (feedback: AssistantFeedback | undefined) => void;
        onRegenerate?: () => void;
        showFeedback?: boolean;
    }

    let { align = 'start', content, feedback, onFeedback, onRegenerate, showFeedback = false }: Props = $props();
    const clipboard = new UseClipboard({
        delay: 1500
    });

    async function updateFeedback(value: AssistantFeedback): Promise<void> {
        const updatedFeedback = feedback === value ? undefined : value;
        onFeedback?.(updatedFeedback);
        if (updatedFeedback) {
            await submitFeatureUsage(updatedFeedback === 'helpful' ? 'assistant.ResponseHelpful' : 'assistant.ResponseNotHelpful').catch(() => undefined);
        }
    }
</script>

<Tooltip.Provider delayDuration={300}>
    <div
        class={[
            'flex items-center gap-0.5 pt-1 opacity-100 transition-opacity sm:opacity-0 sm:group-focus-within/message:opacity-100 sm:group-hover/message:opacity-100',
            align === 'end' && 'justify-end'
        ]}
        aria-label="Message actions"
    >
        <Tooltip.Root>
            <Tooltip.Trigger>
                {#snippet child({ props })}
                    <Button {...props} aria-label="Copy message" onclick={() => void clipboard.copy(content)} size="icon-xs" variant="ghost">
                        {#if clipboard.copied}<Check aria-hidden="true" />{:else}<Clipboard aria-hidden="true" />{/if}
                    </Button>
                {/snippet}
            </Tooltip.Trigger>
            <Tooltip.Content>{clipboard.copied ? 'Copied' : 'Copy message'}</Tooltip.Content>
        </Tooltip.Root>

        {#if onRegenerate}
            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} aria-label="Regenerate response" onclick={onRegenerate} size="icon-xs" variant="ghost">
                            <RotateCcw aria-hidden="true" />
                        </Button>
                    {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>Regenerate response</Tooltip.Content>
            </Tooltip.Root>
        {/if}

        {#if showFeedback}
            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button
                            {...props}
                            aria-label="Good response"
                            aria-pressed={feedback === 'helpful'}
                            onclick={() => void updateFeedback('helpful')}
                            size="icon-xs"
                            variant={feedback === 'helpful' ? 'secondary' : 'ghost'}
                        >
                            <ThumbsUp aria-hidden="true" />
                        </Button>
                    {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>Good response</Tooltip.Content>
            </Tooltip.Root>
            <Tooltip.Root>
                <Tooltip.Trigger>
                    {#snippet child({ props })}
                        <Button
                            {...props}
                            aria-label="Poor response"
                            aria-pressed={feedback === 'not-helpful'}
                            onclick={() => void updateFeedback('not-helpful')}
                            size="icon-xs"
                            variant={feedback === 'not-helpful' ? 'secondary' : 'ghost'}
                        >
                            <ThumbsDown aria-hidden="true" />
                        </Button>
                    {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>Poor response</Tooltip.Content>
            </Tooltip.Root>
        {/if}
    </div>
</Tooltip.Provider>
