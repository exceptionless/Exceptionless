<script lang="ts">
    import { Response } from '$comp/ai-elements/response';
    import { Spinner } from '$comp/ui/spinner';
    import Bot from '@lucide/svelte/icons/bot';

    import type { AssistantChatMessage, AssistantFeedback } from '../models';

    import { normalizeAssistantUrl } from '../assistant-links';
    import AssistantMessageActions from './assistant-message-actions.svelte';
    import AssistantToolActivity from './assistant-tool-activity.svelte';

    interface Props {
        isLast?: boolean;
        isStreaming?: boolean;
        message: AssistantChatMessage;
        onFeedback?: (feedback: AssistantFeedback | undefined) => void;
        onRegenerate?: () => void;
    }

    let { isLast = false, isStreaming = false, message, onFeedback, onRegenerate }: Props = $props();
</script>

{#if message.role === 'user'}
    <article class="group/message ml-10 flex flex-col items-end" aria-label="You">
        <div class="bg-muted max-w-full rounded-2xl rounded-br-sm px-3 py-2 text-sm wrap-anywhere whitespace-pre-wrap">{message.content}</div>
        <AssistantMessageActions align="end" content={message.content} />
    </article>
{:else}
    <article class="group/message flex gap-2" aria-label="Exie">
        <div class="bg-primary/10 text-primary mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-lg">
            <Bot aria-hidden="true" class="size-4" />
        </div>
        <div class="min-w-0 flex-1">
            {#each message.tools as tool (tool.id)}
                <AssistantToolActivity {tool} />
            {/each}

            {#if message.content}
                <Response
                    class="text-sm"
                    content={message.content}
                    isAnimating={isStreaming}
                    mode={isStreaming ? 'streaming' : 'static'}
                    urlTransform={normalizeAssistantUrl}
                />
            {:else if isStreaming}
                <div class="text-muted-foreground flex items-center gap-2 py-1 text-sm"><Spinner /> Exie is thinking…</div>
            {/if}

            {#if message.content && !isStreaming}
                <AssistantMessageActions
                    content={message.content}
                    feedback={message.feedback}
                    {onFeedback}
                    onRegenerate={isLast ? onRegenerate : undefined}
                    showFeedback={true}
                />
            {/if}
        </div>
    </article>
{/if}
