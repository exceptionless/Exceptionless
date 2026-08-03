<script lang="ts">
    import { page } from '$app/state';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import { accessToken } from '$features/auth/index.svelte';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';
    import Bot from '@lucide/svelte/icons/bot';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import Eraser from '@lucide/svelte/icons/eraser';
    import { tick } from 'svelte';

    import type { AssistantChatMessage, AssistantFeedback } from '../models';

    import { createAssistantChatRequest } from '../assistant-request';
    import { type AssistantStreamEvent, readAssistantStream } from '../assistant-stream';
    import { assistantToolResultFailed } from '../assistant-tool-result';
    import AssistantComposer from './assistant-composer.svelte';
    import AssistantMessage from './assistant-message.svelte';
    import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';

    interface Props {
        accessMessage?: string;
        hasAccess?: boolean;
        open?: boolean;
        organizationId?: string;
        path?: string;
        projectId?: string;
        upgradeRequired?: boolean;
    }

    let { accessMessage, hasAccess = true, open = $bindable(false), organizationId, path, projectId, upgradeRequired = false }: Props = $props();
    let messages = $state<AssistantChatMessage[]>([]);
    let conversationId = $state(crypto.randomUUID());
    let conversationOrganizationId = $state<string>();
    let prompt = $state('');
    let errorMessage = $state<string>();
    let isStreaming = $state(false);
    let isNearBottom = $state(true);
    let showScrollToBottom = $state(false);
    let conversationElement = $state<HTMLDivElement>();
    let abortController: AbortController | undefined;
    let latestAssistantMessage = $derived(messages.filter((message) => message.role === 'assistant').at(-1));

    const suggestions = [
        'What are my top errors in the last 24 hours?',
        'Which open stacks occurred most recently?',
        'Explain what I can investigate on this page.'
    ];
    $effect(() => {
        if (open && conversationElement) {
            void scrollToLatest('auto', true);
        }
    });

    $effect(() => {
        if (!hasAccess) {
            stopStreaming();
        }
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        if (conversationOrganizationId !== currentOrganizationId) {
            stopStreaming();
            messages = [];
            errorMessage = undefined;
            prompt = '';
            conversationId = crypto.randomUUID();
            conversationOrganizationId = currentOrganizationId;
        }
    });

    async function submitPrompt(value = prompt): Promise<void> {
        const content = value.trim();
        if (!content || isStreaming) {
            return;
        }

        prompt = '';
        errorMessage = undefined;
        const userMessage: AssistantChatMessage = { content, id: crypto.randomUUID(), role: 'user', tools: [] };
        const assistantMessage: AssistantChatMessage = { content: '', id: crypto.randomUUID(), role: 'assistant', tools: [] };
        const history = [...messages, userMessage];
        messages = [...history, assistantMessage];
        await streamResponse(history, assistantMessage);
    }

    async function regenerateResponse(assistantMessageId: string): Promise<void> {
        if (isStreaming) {
            return;
        }

        const assistantMessageIndex = messages.findIndex((message) => message.id === assistantMessageId && message.role === 'assistant');
        if (assistantMessageIndex < 1) {
            return;
        }

        const userMessageIndex = messages.findLastIndex((message, index) => index < assistantMessageIndex && message.role === 'user');
        if (userMessageIndex < 0) {
            return;
        }

        errorMessage = undefined;
        const history = messages.slice(0, userMessageIndex + 1);
        const replacement: AssistantChatMessage = { content: '', id: crypto.randomUUID(), role: 'assistant', tools: [] };
        messages = [...history, replacement];
        await streamResponse(history, replacement);
    }

    async function streamResponse(history: AssistantChatMessage[], assistantMessage: AssistantChatMessage): Promise<void> {
        isStreaming = true;
        abortController = new AbortController();
        await scrollToLatest('smooth', true);

        try {
            const response = await fetch('/api/v2/assistant/chat', {
                body: JSON.stringify(
                    createAssistantChatRequest(history, conversationId, organizationId, path ?? `${page.url.pathname}${page.url.search}`, projectId)
                ),
                headers: {
                    Authorization: `Bearer ${accessToken.current}`,
                    'Content-Type': 'application/json'
                },
                method: 'POST',
                signal: abortController.signal
            });

            if (!response.ok) {
                const problem = (await response.json().catch(() => undefined)) as undefined | { detail?: string; title?: string };
                throw new Error(problem?.detail ?? problem?.title ?? `The assistant returned status ${response.status}.`);
            }

            if (!response.body) {
                throw new Error('The assistant returned an empty response.');
            }

            await readAssistantStream(response.body, async (event) => {
                applyStreamEvent(assistantMessage.id, event);
                await scrollToLatest('auto');
            });
        } catch (error) {
            if (error instanceof DOMException && error.name === 'AbortError') {
                return;
            }

            errorMessage = error instanceof Error ? error.message : 'Exie could not complete this request.';
        } finally {
            isStreaming = false;
            abortController = undefined;
            await scrollToLatest('auto');
        }
    }

    function applyStreamEvent(assistantMessageId: string, event: AssistantStreamEvent): void {
        messages = messages.map((message) => {
            if (message.id !== assistantMessageId) {
                return message;
            }

            if (event.type === 'text_delta') {
                return { ...message, content: message.content + (event.text ?? '') };
            }

            if (event.type === 'tool_call' && event.tool_call_id && event.tool_name) {
                return {
                    ...message,
                    tools: [
                        ...message.tools,
                        {
                            arguments: event.arguments ?? '{}',
                            id: event.tool_call_id,
                            name: event.tool_name,
                            status: 'running' as const
                        }
                    ]
                };
            }

            if (event.type === 'tool_result' && event.tool_call_id) {
                const status = assistantToolResultFailed(event.result) ? ('failed' as const) : ('complete' as const);
                return {
                    ...message,
                    tools: message.tools.map((tool) => (tool.id === event.tool_call_id ? { ...tool, result: event.result, status } : tool))
                };
            }

            return message;
        });

        if (event.type === 'error') {
            errorMessage = event.message ?? 'Exie could not complete this request.';
        }
    }

    function handleInteractOutside(event: PointerEvent): void {
        if (event.target instanceof Element && event.target.closest('[data-assistant-trigger]')) {
            event.preventDefault();
        }
    }

    function stopStreaming(): void {
        abortController?.abort();
    }

    function clearConversation(): void {
        stopStreaming();
        messages = [];
        conversationId = crypto.randomUUID();
        errorMessage = undefined;
        prompt = '';
        isNearBottom = true;
        showScrollToBottom = false;
    }

    function handleConversationScroll(): void {
        if (!conversationElement) {
            return;
        }

        const distanceFromBottom = conversationElement.scrollHeight - conversationElement.scrollTop - conversationElement.clientHeight;
        isNearBottom = distanceFromBottom < 80;
        showScrollToBottom = !isNearBottom;
    }

    function setMessageFeedback(messageId: string, feedback: AssistantFeedback | undefined): void {
        messages = messages.map((message) => (message.id === messageId ? { ...message, feedback } : message));
    }

    async function scrollToLatest(behavior: 'auto' | 'smooth' = 'smooth', force = false): Promise<void> {
        if (!force && !isNearBottom) {
            showScrollToBottom = true;
            return;
        }

        await tick();
        conversationElement?.scrollTo({ behavior, top: conversationElement.scrollHeight });
        isNearBottom = true;
        showScrollToBottom = false;
    }
</script>

<Sheet.Root bind:open>
    <Sheet.Content
        data-assistant-panel
        class="bg-background top-16! bottom-0! h-auto! w-full gap-0 sm:max-w-120!"
        onInteractOutside={handleInteractOutside}
        overlayProps={{ class: 'top-16! bg-black/5 dark:bg-black/30' }}
        preventScroll={false}
    >
        <Sheet.Header class="border-b pr-14">
            <div class="flex items-center gap-2">
                <div class="bg-primary/10 text-primary flex size-8 items-center justify-center rounded-lg">
                    <Bot aria-hidden="true" />
                </div>
                <div>
                    <Sheet.Title level={2}>Exie</Sheet.Title>
                    <Sheet.Description>Your Exceptionless assistant.</Sheet.Description>
                </div>
            </div>
            {#if hasAccess && messages.length > 0}
                <Button
                    aria-label="Clear conversation"
                    class="absolute top-3 right-12"
                    onclick={clearConversation}
                    size="icon-sm"
                    title="Clear conversation"
                    variant="ghost"
                >
                    <Eraser aria-hidden="true" />
                </Button>
            {/if}
        </Sheet.Header>

        <div class="relative min-h-0 flex-1">
            {#if !hasAccess}
                <AssistantUpgradeRequired message={accessMessage} {organizationId} {upgradeRequired} />
            {:else}
                <div
                    bind:this={conversationElement}
                    class="h-full overflow-y-auto px-4 py-5"
                    onscroll={handleConversationScroll}
                    role="log"
                    aria-live="polite"
                    aria-label="Conversation with Exie"
                >
                    {#if messages.length === 0}
                        <div class="flex h-full flex-col items-center justify-center gap-6 text-center">
                            <div class="bg-primary/10 text-primary flex size-12 items-center justify-center rounded-xl">
                                <Bot aria-hidden="true" class="size-7" />
                            </div>
                            <div class="max-w-72">
                                <h3 class="text-base font-semibold">Hi, I’m Exie. How can I help?</h3>
                                <p class="text-muted-foreground mt-1 text-sm">
                                    I can use tools to investigate your Exceptionless data and make the stack changes you request. I’ll automatically use the
                                    page or detail panel you’re viewing as context.
                                </p>
                            </div>
                            <div class="grid w-full gap-2">
                                {#each suggestions as suggestion (suggestion)}
                                    <Button
                                        class="h-auto justify-start px-3 py-2 text-left whitespace-normal"
                                        onclick={() => void submitPrompt(suggestion)}
                                        variant="outline"
                                    >
                                        {suggestion}
                                    </Button>
                                {/each}
                            </div>
                        </div>
                    {:else}
                        <div class="flex flex-col gap-5">
                            {#each messages as message (message.id)}
                                <AssistantMessage
                                    isLast={message === messages.at(-1)}
                                    isStreaming={isStreaming && message === messages.at(-1)}
                                    {message}
                                    onFeedback={(feedback) => setMessageFeedback(message.id, feedback)}
                                    onRegenerate={() => void regenerateResponse(message.id)}
                                />
                            {/each}
                        </div>
                    {/if}
                </div>
            {/if}
            {#if hasAccess && showScrollToBottom}
                <Button
                    aria-label="Scroll to latest message"
                    class="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full shadow-md"
                    onclick={() => void scrollToLatest('smooth', true)}
                    size="icon-sm"
                    variant="secondary"
                >
                    <ArrowDown aria-hidden="true" />
                </Button>
            {/if}
        </div>

        {#if hasAccess}
            <Sheet.Footer class="bg-background gap-2 border-t p-3">
                {#if errorMessage}
                    <Alert.Root variant="destructive">
                        <CircleAlert aria-hidden="true" />
                        <Alert.Title>Exie couldn’t finish that response</Alert.Title>
                        <Alert.Description>{errorMessage}</Alert.Description>
                        {#if latestAssistantMessage && !isStreaming}
                            <Alert.Action>
                                <Button onclick={() => void regenerateResponse(latestAssistantMessage!.id)} size="xs" variant="outline">Retry</Button>
                            </Alert.Action>
                        {/if}
                    </Alert.Root>
                {/if}
                <AssistantComposer bind:value={prompt} {isStreaming} onStop={stopStreaming} onSubmit={() => void submitPrompt()} />
                <p class="text-muted-foreground text-center text-xs">AI can make mistakes. Check important changes.</p>
            </Sheet.Footer>
        {/if}
    </Sheet.Content>
</Sheet.Root>
