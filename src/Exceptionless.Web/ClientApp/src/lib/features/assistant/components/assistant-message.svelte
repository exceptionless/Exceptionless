<script lang="ts">
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import CircleStop from '@lucide/svelte/icons/circle-stop';
    import CornerDownRight from '@lucide/svelte/icons/corner-down-right';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    import { Response } from '#comp/ai-elements/response/index.ts';
    import * as Bubble from '#comp/ui/bubble/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Marker from '#comp/ui/marker/index.ts';
    import * as Message from '#comp/ui/message/index.ts';
    import * as Trace from '#comp/ui/trace/index.ts';

    import type { AssistantChatMessage, AssistantFeedback, AssistantFeedbackReason, AssistantSuggestedAction } from '../models';

    import { addAssistantResourceLinks, createAssistantUrlTransform } from '../assistant-links';
    import { getAssistantMessageSegments } from '../assistant-message-parts';
    import { getAssistantToolProjectNames } from '../assistant-tools';
    import AssistantMessageActions from './assistant-message-actions.svelte';
    import AssistantToolTrace from './assistant-tool-trace.svelte';

    interface Props {
        isLast?: boolean;
        isStreaming?: boolean;
        message: AssistantChatMessage;
        onCopy?: () => void;
        onEdit?: () => void;
        onFeedback?: (feedback: AssistantFeedback | undefined, reason?: AssistantFeedbackReason) => void;
        onRegenerate?: () => Promise<void> | void;
        onSuggestedAction?: (action: AssistantSuggestedAction) => void;
        showSuggestedActions?: boolean;
        suggestionsDisabled?: boolean;
    }

    let {
        isLast = false,
        isStreaming = false,
        message,
        onCopy,
        onEdit,
        onFeedback,
        onRegenerate,
        onSuggestedAction,
        showSuggestedActions = false,
        suggestionsDisabled = false
    }: Props = $props();
    let segments = $derived(getAssistantMessageSegments(message));
    let urlTransform = $derived(createAssistantUrlTransform(message.tools));
    let projectNames = $derived(getAssistantToolProjectNames(message.tools));
    let lastSegment = $derived(segments.at(-1));
    // Exie is between steps: nothing has streamed yet, or every tool so far has finished.
    let isThinking = $derived(isStreaming && (!lastSegment || (lastSegment.type === 'tools' && lastSegment.tools.every((tool) => tool.status !== 'running'))));
</script>

{#if message.role === 'user'}
    <Message.Root align="end" aria-label="You" class="group/message">
        <Message.Content class="items-end gap-1">
            {#if message.isSuggestedAction}
                <span class="text-muted-foreground flex items-center gap-1 text-xs">
                    <CornerDownRight aria-hidden="true" class="size-3" />
                    Suggested action
                </span>
            {/if}
            <Bubble.Root align="end" variant="muted">
                <Bubble.Content class="whitespace-pre-wrap">{message.content}</Bubble.Content>
            </Bubble.Root>
            <AssistantMessageActions align="end" content={message.content} {onCopy} {onEdit} />
        </Message.Content>
    </Message.Root>
{:else}
    <Message.Root aria-busy={isStreaming} aria-label="Exie" class="group/message">
        <Message.Content class="gap-3">
            {#each segments as segment, index (segment.key)}
                {#if segment.type === 'tools'}
                    <AssistantToolTrace {isStreaming} {projectNames} tools={segment.tools} />
                {:else}
                    {@const isStreamingText = isStreaming && index === segments.length - 1}
                    <Response
                        class="text-sm"
                        content={isStreamingText ? segment.text : addAssistantResourceLinks(segment.text, message.tools)}
                        isAnimating={isStreamingText}
                        mode={isStreamingText ? 'streaming' : 'static'}
                        {urlTransform}
                    />
                {/if}
            {/each}

            {#if isThinking}
                <Trace.Root>
                    <Trace.Line status="running">
                        <Trace.Title>{lastSegment ? 'Thinking…' : 'Exie is thinking…'}</Trace.Title>
                    </Trace.Line>
                </Trace.Root>
            {/if}

            {#if message.endReason === 'stopped'}
                <Marker.Root class="text-xs">
                    <CircleStop aria-hidden="true" />
                    {segments.length > 0 ? 'You stopped this response.' : 'You stopped Exie before it responded.'}
                    {#if isLast && onRegenerate}
                        <Button class="ml-1 h-6 px-2 text-xs" onclick={() => void onRegenerate?.()} size="xs" variant="outline">Retry</Button>
                    {/if}
                </Marker.Root>
            {:else if message.endReason === 'error'}
                <div class="border-destructive/30 bg-destructive/5 text-destructive flex items-start gap-2 rounded-lg border px-3 py-2 text-sm" role="alert">
                    <CircleAlert aria-hidden="true" class="mt-0.5 size-4 shrink-0" />
                    <div class="min-w-0 flex-1">
                        <p class="font-medium">Exie couldn’t finish this response</p>
                        <p class="text-destructive/80 text-xs wrap-anywhere">{message.errorMessage}</p>
                    </div>
                    {#if isLast && onRegenerate}
                        <Button class="shrink-0" onclick={() => void onRegenerate?.()} size="xs" variant="outline">Retry</Button>
                    {/if}
                </div>
            {/if}

            {#if showSuggestedActions && message.suggestedActions?.length && !isStreaming}
                <section aria-label="Suggested next steps" class="flex flex-col gap-1.5">
                    <div class="flex flex-wrap gap-1.5">
                        {#each message.suggestedActions as action (`${action.label}:${action.prompt ?? action.href}`)}
                            <Button
                                class="h-auto min-h-7 max-w-full rounded-full px-3 py-1 text-left text-xs whitespace-normal"
                                disabled={suggestionsDisabled}
                                onclick={() => onSuggestedAction?.(action)}
                                size="sm"
                                variant="outline"
                            >
                                {action.label}
                                {#if action.href}
                                    <ExternalLink aria-hidden="true" class="size-3" data-icon="inline-end" />
                                {/if}
                            </Button>
                        {/each}
                    </div>
                </section>
            {/if}

            {#if message.content && !isStreaming}
                <Message.Footer class="px-0">
                    <AssistantMessageActions
                        content={message.content}
                        feedback={message.feedback}
                        feedbackReason={message.feedbackReason}
                        {onCopy}
                        {onFeedback}
                        onRegenerate={isLast && !message.endReason ? onRegenerate : undefined}
                        showFeedback={true}
                    />
                </Message.Footer>
            {/if}
        </Message.Content>
    </Message.Root>
{/if}
