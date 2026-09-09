<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import { accessToken } from '$features/auth/index.svelte';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';
    import Bot from '@lucide/svelte/icons/bot';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import Eraser from '@lucide/svelte/icons/eraser';
    import Maximize2 from '@lucide/svelte/icons/maximize-2';
    import Minimize2 from '@lucide/svelte/icons/minimize-2';
    import { onDestroy, tick, untrack } from 'svelte';

    import type { AssistantAccessState, AssistantChatMessage, AssistantFeedback, AssistantPromptRequest, AssistantSuggestedAction } from '../models';

    import { createAssistantChatRequest } from '../assistant-request';
    import { type AssistantStreamEvent, readAssistantStream } from '../assistant-stream';
    import {
        type AssistantPromptSource,
        type AssistantStopReason,
        type AssistantTelemetryContext,
        type AssistantTurnOutcome,
        AssistantTurnTelemetry,
        trackAssistantEvent
    } from '../assistant-telemetry';
    import { assistantToolResultFailed } from '../assistant-tool-result';
    import AssistantComposer from './assistant-composer.svelte';
    import AssistantMessage from './assistant-message.svelte';
    import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';

    interface Props {
        accessMessage?: string;
        accessState?: AssistantAccessState;
        collapseHref?: string;
        expandHref?: string;
        minimumPlanId?: string;
        mode?: 'page' | 'sheet';
        onAccessChanged?: () => Promise<void> | void;
        onCollapse?: () => void;
        onRetryAccess?: () => Promise<void> | void;
        open?: boolean;
        organizationId?: string;
        path?: string;
        projectId?: string;
        promptRequest?: AssistantPromptRequest;
    }

    let {
        accessMessage,
        accessState = 'available',
        collapseHref,
        expandHref,
        minimumPlanId,
        mode = 'sheet',
        onAccessChanged,
        onCollapse,
        onRetryAccess,
        open = $bindable(false),
        organizationId,
        path,
        projectId,
        promptRequest
    }: Props = $props();
    let messages = $state<AssistantChatMessage[]>([]);
    let conversationId = $state(createConversationId());
    let conversationOrganizationId = $state<string>();
    let prompt = $state('');
    let errorMessage = $state<string>();
    let isStreaming = $state(false);
    let isNearBottom = $state(true);
    let showToolCalls = $state(false);
    let showScrollToBottom = $state(false);
    let conversationElement = $state<HTMLDivElement>();
    let abortController: AbortController | undefined;
    let handledPromptRequestId: string | undefined;
    let activeTurn: AssistantTurnTelemetry | undefined;
    let lastOutcome: AssistantTurnOutcome | undefined;
    let wasVisible = false;
    let previousMode: 'page' | 'sheet' | undefined;
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
        if (accessState !== 'available') {
            untrack(() => stopStreaming('access_changed'));
        }
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        if (conversationOrganizationId !== currentOrganizationId) {
            untrack(() => {
                if (messages.length > 0) {
                    trackConversationEvent('assistant.ConversationLeft', {
                        reason: 'organization_changed'
                    });
                }
                stopStreaming('organization_changed');
            });
            messages = [];
            errorMessage = undefined;
            prompt = '';
            conversationId = createConversationId();
            conversationOrganizationId = currentOrganizationId;
            lastOutcome = undefined;
        }
    });

    $effect(() => {
        const visible = mode === 'page' || open;
        const currentMode = mode;
        untrack(() => {
            if (visible !== wasVisible) {
                trackConversationEvent(visible ? 'assistant.Opened' : 'assistant.Closed');
            } else if (visible && currentMode !== previousMode) {
                trackConversationEvent('assistant.ViewChanged', {
                    previous_mode: previousMode
                });
            }
            wasVisible = visible;
            previousMode = currentMode;
        });
    });

    onDestroy(() => {
        if (wasVisible) {
            trackConversationEvent('assistant.ConversationLeft', {
                reason: 'component_unmounted'
            });
        }
        stopStreaming('component_unmounted');
    });

    $effect(() => {
        if (
            !open ||
            accessState !== 'available' ||
            isStreaming ||
            !promptRequest ||
            !organizationId ||
            conversationOrganizationId !== organizationId ||
            promptRequest.id === handledPromptRequestId
        ) {
            return;
        }

        handledPromptRequestId = promptRequest.id;
        void submitPrompt(promptRequest.prompt, {
            source: 'queued'
        });
    });

    async function submitPrompt(value = prompt, options: { action?: AssistantSuggestedAction; source?: AssistantPromptSource } = {}): Promise<void> {
        const content = value.trim();
        if (!content || isStreaming) {
            return;
        }

        prompt = '';
        if (content.toLowerCase() === '/tools') {
            showToolCalls = !showToolCalls;
            return;
        }

        errorMessage = undefined;
        const userMessage: AssistantChatMessage = {
            content,
            conversationId,
            id: crypto.randomUUID(),
            isSuggestedAction: options.source === 'suggested_action',
            role: 'user',
            suggestedActionLabel: options.action?.label,
            suggestedActionPath: options.action?.sourcePath,
            tools: []
        };
        const assistantMessage: AssistantChatMessage = {
            content: '',
            conversationId,
            id: crypto.randomUUID(),
            role: 'assistant',
            tools: []
        };
        const history = [...messages, userMessage];
        messages = [...history, assistantMessage];
        await streamResponse(history, assistantMessage, options.source ?? 'composer');
    }

    async function handleSuggestedAction(action: AssistantSuggestedAction, message: AssistantChatMessage): Promise<void> {
        trackAssistantEvent('assistant.SuggestedActionSelected', getTelemetryContext(message), {
            action_label: action.label,
            action_type: action.href ? 'navigation' : 'prompt',
            target_path: action.href?.split(/[?#]/)[0]
        });
        if (action.href) {
            open = false;
            await goto(action.href);
            return;
        }

        await submitPrompt(action.prompt, {
            action,
            source: 'suggested_action'
        });
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

        const source = errorMessage ? 'retry' : 'regenerate';
        const previousConversationId = conversationId;
        trackAssistantEvent('assistant.ResponseRegenerated', getTelemetryContext(messages[assistantMessageIndex]), {
            prompt_source: source
        });
        errorMessage = undefined;
        const history = messages.slice(0, userMessageIndex + 1);
        conversationId = createConversationId();
        const replacement: AssistantChatMessage = {
            content: '',
            conversationId,
            id: crypto.randomUUID(),
            role: 'assistant',
            tools: []
        };
        messages = [...history, replacement];
        await streamResponse(history, replacement, source, {
            previous_conversation_id: previousConversationId,
            retry_of_message_id: assistantMessageId
        });
    }

    async function streamResponse(
        history: AssistantChatMessage[],
        assistantMessage: AssistantChatMessage,
        source: AssistantPromptSource,
        details: Record<string, unknown> = {}
    ): Promise<void> {
        isStreaming = true;
        const controller = new AbortController();
        abortController = controller;
        const requestPath = path ?? `${page.url.pathname}${page.url.search}`;
        const userMessage = history.at(-1)!;
        const telemetry = new AssistantTurnTelemetry(
            {
                ...getTelemetryContext(assistantMessage),
                user_message_id: userMessage.id
            },
            userMessage.content,
            source,
            {
                ...details,
                turn_index: history.filter((message) => message.role === 'user').length
            }
        );
        activeTurn = telemetry;
        lastOutcome = undefined;
        const request = createAssistantChatRequest(history, conversationId, organizationId, requestPath, projectId);

        try {
            await scrollToLatest('smooth', true);
            const response = await fetch('/api/v2/assistant/chat', {
                body: JSON.stringify(request),
                headers: {
                    Authorization: `Bearer ${accessToken.current}`,
                    'Content-Type': 'application/json'
                },
                method: 'POST',
                signal: controller.signal
            });

            if (!response.ok) {
                const problem = response.headers.get('content-type')?.includes('json')
                    ? ((await response.json()) as { detail?: string; title?: string })
                    : undefined;
                const message = problem?.detail ?? problem?.title ?? `The assistant returned status ${response.status}.`;
                telemetry.fail(message, `http_${response.status}`);
                throw new Error(message);
            }

            if (!response.body) {
                throw new Error('The assistant returned an empty response.');
            }

            await readAssistantStream(response.body, async (event) => {
                if (controller.signal.aborted) {
                    return;
                }
                telemetry.observe(event);
                applyStreamEvent(assistantMessage.id, event, requestPath);
                await scrollToLatest('auto');
            });
        } catch (error) {
            if (controller.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) {
                return;
            }

            errorMessage = error instanceof Error ? error.message : 'Exie could not complete this request.';
            telemetry.fail(errorMessage, 'request_error');
        } finally {
            const outcome = telemetry.finish(controller.signal.aborted ? 'user_stopped' : undefined, {
                is_visible: mode === 'page' || open
            });
            if (abortController === controller) {
                lastOutcome = outcome ?? lastOutcome;
                activeTurn = undefined;
                isStreaming = false;
                abortController = undefined;
                await scrollToLatest('auto');
            }
        }
    }

    function applyStreamEvent(assistantMessageId: string, event: AssistantStreamEvent, requestPath: string): void {
        messages = messages.map((message) => {
            if (message.id !== assistantMessageId) {
                return message;
            }

            if (event.type === 'text_delta') {
                return {
                    ...message,
                    content: message.content + (event.text ?? '')
                };
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
                    tools: message.tools.map((tool) =>
                        tool.id === event.tool_call_id
                            ? {
                                  ...tool,
                                  result: event.result,
                                  status
                              }
                            : tool
                    )
                };
            }

            if (event.type === 'suggested_actions') {
                return {
                    ...message,
                    suggestedActions: (event.suggested_actions ?? []).map((action) => ({
                        ...action,
                        sourcePath: requestPath
                    }))
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

    function stopStreaming(reason: AssistantStopReason = 'user_stopped'): void {
        const outcome = activeTurn?.finish(reason, {
            is_visible: mode === 'page' || open
        });
        if (outcome) {
            lastOutcome = outcome;
        }
        abortController?.abort();
        if (!messages.some((message) => message.tools.some((tool) => tool.status === 'running'))) {
            return;
        }

        messages = messages.map((message) => ({
            ...message,
            tools: message.tools.map((tool) =>
                tool.status === 'running'
                    ? {
                          ...tool,
                          status: 'cancelled' as const
                      }
                    : tool
            )
        }));
    }

    function clearConversation(): void {
        trackConversationEvent('assistant.ConversationCleared');
        stopStreaming('conversation_cleared');
        messages = [];
        conversationId = createConversationId();
        errorMessage = undefined;
        prompt = '';
        isNearBottom = true;
        showScrollToBottom = false;
        lastOutcome = undefined;
    }

    function collapseToSidePanel(): void {
        onCollapse?.();
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
        const message = messages.find((message) => message.id === messageId);
        if (!message) {
            return;
        }
        const feature =
            feedback === 'helpful'
                ? 'assistant.ResponseHelpful'
                : feedback === 'not-helpful'
                  ? 'assistant.ResponseNotHelpful'
                  : 'assistant.ResponseFeedbackCleared';
        trackAssistantEvent(feature, getTelemetryContext(message), {
            feedback: feedback ?? 'cleared'
        });
        messages = messages.map((message) =>
            message.id === messageId
                ? {
                      ...message,
                      feedback
                  }
                : message
        );
    }

    function getTelemetryContext(message?: AssistantChatMessage): AssistantTelemetryContext {
        return {
            assistant_message_id: message?.role === 'assistant' ? message.id : undefined,
            conversation_id: message?.conversationId ?? conversationId,
            mode,
            organization_id: conversationOrganizationId ?? organizationId,
            path: (path ?? page.url.pathname).split(/[?#]/)[0],
            project_id: projectId,
            user_message_id: message?.role === 'user' ? message.id : undefined
        };
    }

    function createConversationId(): string {
        // Match the server's Guid.ToString("N") representation for exact log correlation.
        return crypto.randomUUID().replaceAll('-', '');
    }

    function trackConversationEvent(feature: string, details: Record<string, unknown> = {}): void {
        trackAssistantEvent(
            feature,
            {
                ...(activeTurn?.context ?? getTelemetryContext(latestAssistantMessage)),
                mode
            },
            {
                ...details,
                is_streaming: isStreaming,
                last_feedback: latestAssistantMessage?.feedback,
                last_outcome: lastOutcome,
                message_count: messages.length
            }
        );
    }

    function handlePageHide(): void {
        if (wasVisible || messages.length > 0) {
            trackConversationEvent('assistant.PageLeft');
        }
    }

    async function scrollToLatest(behavior: 'auto' | 'smooth' = 'smooth', force = false): Promise<void> {
        if (!force && !isNearBottom) {
            showScrollToBottom = true;
            return;
        }

        await tick();
        conversationElement?.scrollTo({
            behavior,
            top: conversationElement.scrollHeight
        });
        isNearBottom = true;
        showScrollToBottom = false;
    }
</script>

<svelte:window onpagehide={handlePageHide} />

{#snippet conversation()}
    <div class="relative min-h-0 flex-1">
        {#if accessState !== 'available'}
            <AssistantUpgradeRequired {accessState} message={accessMessage} {minimumPlanId} {onAccessChanged} onRetry={onRetryAccess} {organizationId} />
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
                            <H3 class="text-base">Hi, I’m Exie. How can I help?</H3>
                            <Muted class="mt-1">
                                I can use tools to investigate your Exceptionless data and make the stack changes you request. I’ll automatically use the page
                                or detail panel you’re viewing as context.
                            </Muted>
                        </div>
                        <div class="grid w-full max-w-2xl gap-2">
                            {#each suggestions as suggestion (suggestion)}
                                <Button
                                    class="h-auto justify-start px-3 py-2 text-left whitespace-normal"
                                    onclick={() =>
                                        void submitPrompt(suggestion, {
                                            source: 'starter'
                                        })}
                                    variant="outline"
                                >
                                    {suggestion}
                                </Button>
                            {/each}
                        </div>
                    </div>
                {:else}
                    <div class="mx-auto flex w-full max-w-4xl flex-col gap-5">
                        {#each messages as message (message.id)}
                            <AssistantMessage
                                isLast={message === messages.at(-1)}
                                isStreaming={isStreaming && message === messages.at(-1)}
                                {message}
                                onCopy={() =>
                                    trackAssistantEvent('assistant.MessageCopied', getTelemetryContext(message), {
                                        role: message.role
                                    })}
                                onFeedback={(feedback) => setMessageFeedback(message.id, feedback)}
                                onRegenerate={() => regenerateResponse(message.id)}
                                onSuggestedAction={(action) => void handleSuggestedAction(action, message)}
                                {showToolCalls}
                                suggestionsDisabled={isStreaming}
                            />
                        {/each}
                    </div>
                {/if}
            </div>
        {/if}
        {#if accessState === 'available' && showScrollToBottom}
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
{/snippet}

{#snippet composer()}
    {#if accessState === 'available'}
        <div class="bg-background flex flex-col gap-2 border-t p-3">
            <div class="mx-auto flex w-full max-w-4xl flex-col gap-2">
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
                <AssistantComposer
                    bind:value={prompt}
                    {isStreaming}
                    onStop={() => stopStreaming()}
                    onSubmit={(value) => void submitPrompt(value)}
                    {showToolCalls}
                />
                <Muted class="text-center text-xs">AI can make mistakes. Check important changes.</Muted>
            </div>
        </div>
    {/if}
{/snippet}

{#if mode === 'page'}
    <section class="bg-background flex min-h-0 flex-1 flex-col overflow-hidden" data-assistant-page>
        <header class="flex items-center justify-between gap-3 border-b p-4">
            <div class="flex items-center gap-2">
                <div class="bg-primary/10 text-primary flex size-8 items-center justify-center rounded-lg">
                    <Bot aria-hidden="true" />
                </div>
                <div>
                    <H3 class="text-base">Exie</H3>
                    <Muted class="text-sm">Your Exceptionless assistant.</Muted>
                </div>
            </div>
            <div class="flex items-center gap-1">
                {#if accessState === 'available'}
                    <Button
                        aria-label="Clear conversation"
                        disabled={messages.length === 0}
                        onclick={clearConversation}
                        size="icon-sm"
                        title="Clear conversation"
                        variant="ghost"
                    >
                        <Eraser aria-hidden="true" />
                    </Button>
                {/if}
                {#if collapseHref}
                    <Button
                        aria-label="Collapse Exie to side panel"
                        href={collapseHref}
                        onclick={collapseToSidePanel}
                        size="icon-sm"
                        title="Collapse to side panel"
                        variant="ghost"
                    >
                        <Minimize2 aria-hidden="true" />
                    </Button>
                {/if}
            </div>
        </header>
        {@render conversation()}
        {@render composer()}
    </section>
{:else}
    <Sheet.Root bind:open>
        <Sheet.Content
            data-assistant-panel
            class="bg-background top-16! bottom-0! h-auto! w-full gap-0 sm:max-w-120!"
            onInteractOutside={handleInteractOutside}
            overlayProps={{
                class: 'top-16! bg-black/5 dark:bg-black/30 supports-backdrop-filter:backdrop-blur-[0.5px]'
            }}
            preventScroll={false}
        >
            <Sheet.Header class="border-b pr-24">
                <div class="flex items-center gap-2">
                    <div class="bg-primary/10 text-primary flex size-8 items-center justify-center rounded-lg">
                        <Bot aria-hidden="true" />
                    </div>
                    <div>
                        <Sheet.Title level={2}>Exie</Sheet.Title>
                        <Sheet.Description>Your Exceptionless assistant.</Sheet.Description>
                    </div>
                </div>
                <div class="absolute top-3 right-12 flex items-center gap-1">
                    {#if accessState === 'available' && messages.length > 0}
                        <Button aria-label="Clear conversation" onclick={clearConversation} size="icon-sm" title="Clear conversation" variant="ghost">
                            <Eraser aria-hidden="true" />
                        </Button>
                    {/if}
                    {#if expandHref}
                        <Button aria-label="Expand Exie to full page" href={expandHref} size="icon-sm" title="Expand to full page" variant="ghost">
                            <Maximize2 aria-hidden="true" />
                        </Button>
                    {/if}
                </div>
            </Sheet.Header>
            {@render conversation()}
            {@render composer()}
        </Sheet.Content>
    </Sheet.Root>
{/if}
