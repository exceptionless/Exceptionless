<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ArrowRight from '@lucide/svelte/icons/arrow-right';
    import Maximize2 from '@lucide/svelte/icons/maximize-2';
    import Minimize2 from '@lucide/svelte/icons/minimize-2';
    import SquarePen from '@lucide/svelte/icons/square-pen';
    import { onDestroy, tick, untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Empty from '#comp/ui/empty/index.ts';
    import * as MessageScroller from '#comp/ui/message-scroller/index.ts';
    import * as Sheet from '#comp/ui/sheet/index.ts';
    import * as Tooltip from '#comp/ui/tooltip/index.ts';
    import { accessToken } from '#features/auth/index.svelte.ts';
    import { getApiUrl } from '#shared/api/urls.ts';

    import type {
        AssistantAccessState,
        AssistantChatMessage,
        AssistantConversation,
        AssistantConversationSharingSettings,
        AssistantFeedback,
        AssistantFeedbackReason,
        AssistantPromptRequest,
        AssistantSuggestedAction
    } from '../models';

    import { applyAssistantStreamEvent, DEFAULT_ASSISTANT_ERROR_MESSAGE, endAssistantMessage } from '../assistant-message-parts';
    import { createAssistantChatRequest } from '../assistant-request';
    import { type AssistantContext, getAssistantStarters } from '../assistant-starters';
    import { readAssistantStream } from '../assistant-stream';
    import {
        type AssistantPromptSource,
        type AssistantStopReason,
        type AssistantTelemetryContext,
        type AssistantTurnOutcome,
        AssistantTurnTelemetry,
        trackAssistantEvent
    } from '../assistant-telemetry';
    import {
        deleteAssistantConversation,
        getAssistantConversationScope,
        getAssistantConversationTitle,
        loadAssistantConversations,
        saveAssistantConversation
    } from '../conversation-history';
    import AssistantComposer from './assistant-composer.svelte';
    import AssistantConversationSharing from './assistant-conversation-sharing.svelte';
    import AssistantHistoryMenu from './assistant-history-menu.svelte';
    import AssistantMessage from './assistant-message.svelte';
    import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';
    import ExieAvatar from './exie-avatar.svelte';

    interface Props {
        accessMessage?: string;
        accessState?: AssistantAccessState;
        collapseHref?: string;
        /** What the user is looking at, shown as removable context in the composer. */
        context?: AssistantContext;
        conversationSharing?: AssistantConversationSharingSettings;
        expandHref?: string;
        minimumPlanId?: string;
        mode?: 'page' | 'sheet';
        onAccessChanged?: () => Promise<void> | void;
        onCollapse?: () => void;
        onConversationSharingChange?: (enabled: boolean | null) => Promise<AssistantConversationSharingSettings>;
        onRetryAccess?: () => Promise<void> | void;
        open?: boolean;
        organizationId?: string;
        path?: string;
        projectId?: string;
        promptRequest?: AssistantPromptRequest;
        /** Scopes the locally saved chat history to the signed-in user. */
        userId?: string;
    }

    let {
        accessMessage,
        accessState = 'available',
        collapseHref,
        context,
        conversationSharing,
        expandHref,
        minimumPlanId,
        mode = 'sheet',
        onAccessChanged,
        onCollapse,
        onConversationSharingChange,
        onRetryAccess,
        open = $bindable(false),
        organizationId,
        path,
        projectId,
        promptRequest,
        userId
    }: Props = $props();

    /** A restored chat older than this starts fresh, since its page context is likely stale. */
    const RESTORE_WINDOW_MS = 12 * 60 * 60 * 1000;

    let messages = $state<AssistantChatMessage[]>([]);
    let threadId = $state(createConversationId());
    let conversationId = $state(createConversationId());
    let conversationOrganizationId = $state<string>();
    let conversations = $state<AssistantConversation[]>([]);
    let historyScope = $state<string>();
    let prompt = $state('');
    let editingMessageId = $state<string>();
    let contextDismissed = $state(false);
    let contextPath = $state<string>();
    let isStreaming = $state(false);
    let streamingMessageId = $state<string>();
    let announcement = $state('');
    let sharingSettings = $state<AssistantConversationSharingSettings>();
    let sharingSuppressed = $state(false);
    let sharingError = $state<string>();
    let isSavingSharing = $state(false);
    let requestedSharing = $state<boolean | null>(null);
    let composer = $state<ReturnType<typeof AssistantComposer>>();
    const isSharingEnabled = $derived(sharingSettings?.enabled === true && !sharingSuppressed);
    let abortController: AbortController | undefined;
    let handledPromptRequestId: string | undefined;
    let activeTurn: AssistantTurnTelemetry | undefined;
    let lastOutcome: AssistantTurnOutcome | undefined;
    let wasVisible = false;
    let previousMode: 'page' | 'sheet' | undefined;
    let latestAssistantMessage = $derived(messages.filter((message) => message.role === 'assistant').at(-1));
    let starters = $derived(getAssistantStarters(contextDismissed ? undefined : context));

    $effect(() => {
        sharingSettings = conversationSharing;
        if (conversationSharing?.enabled !== true) {
            activeTurn?.disableFullLogging();
        }
    });

    $effect(() => {
        if (accessState !== 'available') {
            untrack(() => stopStreaming('access_changed'));
        }
    });

    // A new page brings new context, so a dismissed context chip comes back.
    $effect(() => {
        const currentPath = path;
        untrack(() => {
            if (currentPath !== contextPath) {
                contextPath = currentPath;
                contextDismissed = false;
            }
        });
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        const currentScope = getAssistantConversationScope(userId, currentOrganizationId);
        if (conversationOrganizationId !== currentOrganizationId) {
            untrack(() => {
                if (messages.length > 0) {
                    trackConversationEvent('assistant.ConversationLeft', {
                        reason: 'organization_changed'
                    });
                }
                stopStreaming('organization_changed');
            });
            resetConversation();
            conversationOrganizationId = currentOrganizationId;
            historyScope = undefined;
        }

        if (currentScope !== untrack(() => historyScope)) {
            historyScope = currentScope;
            untrack(() => restoreRecentConversation(currentScope));
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

    $effect(() => {
        if (mode === 'page' && accessState === 'available' && composer) {
            void tick().then(() => composer?.focus());
        }
    });

    onDestroy(() => {
        if (wasVisible) {
            trackConversationEvent('assistant.ConversationLeft', {
                reason: 'component_unmounted'
            });
        }
        stopStreaming('component_unmounted');
        persistConversation();
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

    function cancelEdit(): void {
        editingMessageId = undefined;
        prompt = '';
        composer?.focus();
    }

    async function changeConversationSharing(enabled: boolean | null) {
        if (!onConversationSharingChange || isSavingSharing) {
            return;
        }
        sharingError = undefined;
        requestedSharing = enabled;
        isSavingSharing = true;
        if (enabled !== true) {
            sharingSuppressed = true;
            activeTurn?.disableFullLogging();
        }
        try {
            sharingSettings = await onConversationSharingChange(enabled);
            sharingSuppressed = !sharingSettings.enabled;
        } catch {
            sharingError = sharingSuppressed
                ? 'Could not save your choice. Sharing is paused on this page. Try again to save it for all devices.'
                : 'Could not save your sharing choice. Please try again.';
        } finally {
            isSavingSharing = false;
        }
    }

    function collapseToSidePanel(): void {
        onCollapse?.();
    }

    function createConversationId(): string {
        // Match the server's Guid.ToString("N") representation for exact log correlation.
        return crypto.randomUUID().replaceAll('-', '');
    }

    function createMessage(role: AssistantChatMessage['role'], content: string): AssistantChatMessage {
        return {
            content,
            conversationId,
            id: crypto.randomUUID(),
            parts: content
                ? [
                      {
                          text: content,
                          type: 'text'
                      }
                  ]
                : [],
            role,
            tools: []
        };
    }

    function deleteConversation(id: string): void {
        if (!historyScope) {
            return;
        }

        conversations = deleteAssistantConversation(historyScope, id);
        if (id === threadId && !isStreaming) {
            resetConversation();
        }
    }

    function editMessage(messageId: string): void {
        const message = messages.find((item) => item.id === messageId && item.role === 'user');
        if (!message || isStreaming) {
            return;
        }

        editingMessageId = messageId;
        prompt = message.content;
        void tick().then(() => composer?.focus());
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

    function handleInteractOutside(event: PointerEvent): void {
        if (event.target instanceof Element && event.target.closest('[data-assistant-trigger]')) {
            event.preventDefault();
        }
    }

    function handleOpenAutoFocus(event: Event): void {
        if (accessState === 'available') {
            event.preventDefault();
            composer?.focus();
        }
    }

    function handlePageHide(): void {
        if (wasVisible || messages.length > 0) {
            trackConversationEvent('assistant.PageLeft');
        }

        persistConversation();
    }

    async function handleSuggestedAction(action: AssistantSuggestedAction, message: AssistantChatMessage): Promise<void> {
        trackAssistantEvent('assistant.SuggestedActionSelected', getTelemetryContext(message), {
            action_type: action.href ? 'navigation' : 'prompt'
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

    function persistConversation(): void {
        if (!historyScope || messages.length === 0) {
            return;
        }

        // A response still streaming when the page goes away is saved as stopped.
        const savedMessages = messages.map((message) => (message.id === streamingMessageId ? endAssistantMessage(message, 'stopped') : message));
        conversations = saveAssistantConversation(historyScope, {
            id: threadId,
            messages: savedMessages,
            title: getAssistantConversationTitle(savedMessages),
            updatedAt: Date.now()
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

        const source = messages[assistantMessageIndex]?.endReason ? 'retry' : 'regenerate';
        const previousConversationId = conversationId;
        trackAssistantEvent('assistant.ResponseRegenerated', getTelemetryContext(messages[assistantMessageIndex]), {
            prompt_source: source
        });
        const history = messages.slice(0, userMessageIndex + 1);
        conversationId = createConversationId();
        const replacement = createMessage('assistant', '');
        messages = [...history, replacement];
        await streamResponse(history, replacement, source, {
            previous_conversation_id: previousConversationId,
            retry_of_message_id: assistantMessageId
        });
    }

    function resetConversation(): void {
        messages = [];
        threadId = createConversationId();
        conversationId = createConversationId();
        prompt = '';
        editingMessageId = undefined;
        lastOutcome = undefined;
    }

    function restoreConversation(id: string): void {
        const conversation = conversations.find((item) => item.id === id);
        if (!conversation || isStreaming) {
            return;
        }

        persistConversation();
        messages = conversation.messages;
        threadId = conversation.id;
        conversationId = conversation.messages.findLast((message) => message.conversationId)?.conversationId ?? createConversationId();
        editingMessageId = undefined;
        prompt = '';
        lastOutcome = undefined;
        void tick().then(() => composer?.focus());
    }

    function restoreRecentConversation(scope: string | undefined): void {
        conversations = scope ? loadAssistantConversations(scope) : [];
        const recent = conversations[0];
        if (messages.length === 0 && recent && Date.now() - recent.updatedAt < RESTORE_WINDOW_MS) {
            messages = recent.messages;
            threadId = recent.id;
            conversationId = recent.messages.findLast((message) => message.conversationId)?.conversationId ?? conversationId;
        }
    }

    function setMessageFeedback(messageId: string, feedback: AssistantFeedback | undefined, reason?: AssistantFeedbackReason): void {
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
            feedback: feedback ?? 'cleared',
            ...(reason
                ? {
                      feedback_reason: reason
                  }
                : {})
        });
        messages = messages.map((message) =>
            message.id === messageId
                ? {
                      ...message,
                      feedback,
                      feedbackReason: feedback === 'not-helpful' ? reason : undefined
                  }
                : message
        );
        persistConversation();
    }

    function startNewChat(): void {
        if (messages.length === 0) {
            composer?.focus();
            return;
        }

        trackConversationEvent('assistant.ConversationCleared');
        stopStreaming('conversation_cleared');
        persistConversation();
        const previousThreadId = threadId;
        resetConversation();
        announcement = 'Started a new chat.';
        composer?.focus();
        toast('Started a new chat', {
            action: {
                label: 'Undo',
                onClick: () => restoreConversation(previousThreadId)
            },
            duration: 8000
        });
    }

    function stopStreaming(reason: AssistantStopReason = 'user_stopped'): void {
        const outcome = activeTurn?.finish(reason, {
            is_visible: mode === 'page' || open
        });
        if (outcome) {
            lastOutcome = outcome;
        }
        abortController?.abort();
        if (!streamingMessageId) {
            return;
        }

        const stoppedMessageId = streamingMessageId;
        messages = messages.map((message) => (message.id === stoppedMessageId ? endAssistantMessage(message, 'stopped') : message));
        if (reason === 'user_stopped') {
            announcement = 'Stopped Exie’s response.';
        }
    }

    async function streamResponse(
        history: AssistantChatMessage[],
        assistantMessage: AssistantChatMessage,
        source: AssistantPromptSource,
        details: Record<string, unknown> = {}
    ): Promise<void> {
        isStreaming = true;
        streamingMessageId = assistantMessage.id;
        announcement = 'Exie is responding…';
        const controller = new AbortController();
        abortController = controller;
        const requestPath = contextDismissed ? resolve('/(app)') : (path ?? `${page.url.pathname}${page.url.search}`);
        const requestProjectId = contextDismissed ? undefined : projectId;
        const userMessage = history.at(-1)!;
        const telemetry = new AssistantTurnTelemetry(
            {
                ...getTelemetryContext(assistantMessage),
                user_message_id: userMessage.id
            },
            userMessage.content.length,
            source,
            {
                ...details,
                turn_index: history.filter((message) => message.role === 'user').length
            }
        );
        activeTurn = telemetry;
        lastOutcome = undefined;
        const request = createAssistantChatRequest(history, conversationId, organizationId, requestPath, requestProjectId);

        try {
            const response = await fetch(getApiUrl('assistant/chat'), {
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

            if (isSharingEnabled && response.headers.get('X-Exie-Full-Logging') === 'true') {
                telemetry.enableFullLogging(userMessage.content);
            }

            await readAssistantStream(response.body, async (event) => {
                if (controller.signal.aborted) {
                    return;
                }
                telemetry.observe(event);
                updateMessage(assistantMessage.id, (message) => applyAssistantStreamEvent(message, event, requestPath));
            });
        } catch (error) {
            if (controller.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) {
                return;
            }

            const errorMessage = error instanceof Error ? error.message : DEFAULT_ASSISTANT_ERROR_MESSAGE;
            telemetry.fail(errorMessage, 'request_error');
            updateMessage(assistantMessage.id, (message) => endAssistantMessage(message, 'error', errorMessage));
        } finally {
            const outcome = telemetry.finish(controller.signal.aborted ? 'user_stopped' : undefined, {
                is_visible: mode === 'page' || open
            });
            if (abortController === controller) {
                lastOutcome = outcome ?? lastOutcome;
                activeTurn = undefined;
                isStreaming = false;
                streamingMessageId = undefined;
                abortController = undefined;
                const finished = messages.find((message) => message.id === assistantMessage.id);
                if (finished?.endReason === 'error') {
                    announcement = `Exie couldn’t finish the response. ${finished.errorMessage ?? ''}`.trim();
                } else if (!controller.signal.aborted) {
                    announcement = 'Exie finished responding.';
                }
            }

            persistConversation();
        }
    }

    async function submitPrompt(value = prompt, options: { action?: AssistantSuggestedAction; source?: AssistantPromptSource } = {}): Promise<void> {
        const content = value.trim();
        if (!content || isStreaming) {
            return;
        }

        prompt = '';
        const editIndex = editingMessageId ? messages.findIndex((message) => message.id === editingMessageId) : -1;
        const source = editIndex >= 0 ? 'edit' : (options.source ?? 'composer');
        editingMessageId = undefined;
        if (editIndex >= 0) {
            // Editing rewrites history, so the server sees a new conversation.
            conversationId = createConversationId();
        }

        const userMessage: AssistantChatMessage = {
            ...createMessage('user', content),
            isSuggestedAction: options.source === 'suggested_action',
            suggestedActionLabel: options.action?.label,
            suggestedActionPath: options.action?.sourcePath
        };
        const assistantMessage = createMessage('assistant', '');
        const history = [...(editIndex >= 0 ? messages.slice(0, editIndex) : messages), userMessage];
        messages = [...history, assistantMessage];
        // A starter or suggestion button disappears once used, so keep focus in the composer.
        void tick().then(() => composer?.focus());
        await streamResponse(history, assistantMessage, source);
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

    function updateMessage(messageId: string, update: (message: AssistantChatMessage) => AssistantChatMessage): void {
        messages = messages.map((message) => (message.id === messageId ? update(message) : message));
    }
</script>

<svelte:window onpagehide={handlePageHide} />

{#snippet headerActions()}
    <Tooltip.Provider delayDuration={300}>
        <div class="flex items-center gap-0.5">
            {#if accessState === 'available'}
                <AssistantHistoryMenu
                    activeId={messages.length > 0 ? threadId : undefined}
                    {conversations}
                    disabled={!historyScope}
                    onDelete={deleteConversation}
                    onSelect={restoreConversation}
                />
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button {...props} aria-label="New chat" disabled={messages.length === 0} onclick={startNewChat} size="icon-sm" variant="ghost">
                                <SquarePen aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>New chat</Tooltip.Content>
                </Tooltip.Root>
            {/if}
            {#if expandHref}
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button {...props} aria-label="Expand Exie to full page" href={expandHref} size="icon-sm" variant="ghost">
                                <Maximize2 aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>Expand to full page</Tooltip.Content>
                </Tooltip.Root>
            {/if}
            {#if collapseHref}
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button
                                {...props}
                                aria-label="Collapse Exie to side panel"
                                href={collapseHref}
                                onclick={collapseToSidePanel}
                                size="icon-sm"
                                variant="ghost"
                            >
                                <Minimize2 aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>Collapse to side panel</Tooltip.Content>
                </Tooltip.Root>
            {/if}
        </div>
    </Tooltip.Provider>
{/snippet}

{#snippet emptyState()}
    <Empty.Root class="h-full justify-center gap-6 px-6">
        <Empty.Header class="gap-2">
            <Empty.Media>
                <ExieAvatar size="lg" />
            </Empty.Media>
            <Empty.Title class="text-base">Hi, I’m Exie. How can I help?</Empty.Title>
            <Empty.Description class="max-w-80 text-sm">
                I investigate your errors with live Exceptionless data, explain what’s going wrong, and can update stacks when you ask.
            </Empty.Description>
        </Empty.Header>
        <Empty.Content class="w-full max-w-md">
            <ul aria-label="Suggested questions" class="flex w-full flex-col gap-2">
                {#each starters as starter (starter.prompt)}
                    <li>
                        <Button
                            class="group/starter h-auto w-full justify-between gap-3 rounded-xl px-3.5 py-2.5 text-left font-normal whitespace-normal"
                            onclick={() =>
                                void submitPrompt(starter.prompt, {
                                    source: 'starter'
                                })}
                            title={starter.prompt}
                            variant="outline"
                        >
                            <span class="min-w-0">{starter.label}</span>
                            <ArrowRight
                                aria-hidden="true"
                                class="text-muted-foreground size-4 shrink-0 transition-transform group-hover/starter:translate-x-0.5"
                            />
                        </Button>
                    </li>
                {/each}
            </ul>
        </Empty.Content>
    </Empty.Root>
{/snippet}

{#snippet conversation()}
    <div class="relative min-h-0 flex-1">
        {#if accessState !== 'available'}
            <AssistantUpgradeRequired {accessState} message={accessMessage} {minimumPlanId} {onAccessChanged} onRetry={onRetryAccess} {organizationId} />
        {:else if messages.length === 0}
            {@render emptyState()}
        {:else}
            <MessageScroller.Provider autoScroll defaultScrollPosition="last-anchor">
                <MessageScroller.Root>
                    <MessageScroller.Viewport>
                        <MessageScroller.Content
                            aria-busy={isStreaming}
                            aria-label="Conversation with Exie"
                            aria-live={isStreaming ? 'off' : 'polite'}
                            class={['mx-auto w-full gap-6 px-4 py-5', mode === 'page' && 'max-w-3xl']}
                        >
                            {#each messages as message, index (message.id)}
                                {@const isLast = index === messages.length - 1}
                                <MessageScroller.Item messageId={message.id} scrollAnchor={message.role === 'user'}>
                                    <AssistantMessage
                                        {isLast}
                                        isStreaming={isStreaming && message.id === streamingMessageId}
                                        {message}
                                        onCopy={() =>
                                            trackAssistantEvent('assistant.MessageCopied', getTelemetryContext(message), {
                                                role: message.role
                                            })}
                                        onEdit={message.role === 'user' && !isStreaming ? () => editMessage(message.id) : undefined}
                                        onFeedback={(feedback, reason) => setMessageFeedback(message.id, feedback, reason)}
                                        onRegenerate={() => regenerateResponse(message.id)}
                                        onSuggestedAction={(action) => void handleSuggestedAction(action, message)}
                                        showSuggestedActions={message === latestAssistantMessage}
                                        suggestionsDisabled={isStreaming}
                                    />
                                </MessageScroller.Item>
                            {/each}
                        </MessageScroller.Content>
                    </MessageScroller.Viewport>
                    <MessageScroller.Button aria-label="Scroll to latest message" class="shadow-md" />
                </MessageScroller.Root>
            </MessageScroller.Provider>
        {/if}
    </div>
{/snippet}

{#snippet composerArea()}
    {#if accessState === 'available'}
        <div class="bg-background flex flex-col gap-1.5 px-3 pt-2 pb-2.5">
            <div class={['mx-auto flex w-full flex-col gap-1.5', mode === 'page' && 'max-w-3xl']}>
                <AssistantComposer
                    bind:this={composer}
                    bind:contextDismissed
                    bind:value={prompt}
                    {context}
                    isEditing={!!editingMessageId}
                    {isStreaming}
                    onCancelEdit={cancelEdit}
                    onStop={() => stopStreaming()}
                    onSubmit={(value) => void submitPrompt(value)}
                />
                <div class="text-muted-foreground flex items-center justify-between gap-3 px-1 text-[11px]">
                    <span>Exie can make mistakes.<span class="hidden sm:inline">&nbsp;Check important changes.</span></span>
                    <AssistantConversationSharing
                        checked={isSharingEnabled}
                        error={sharingError}
                        isSaving={isSavingSharing}
                        onChange={changeConversationSharing}
                        onRetry={() => changeConversationSharing(requestedSharing)}
                        settings={onConversationSharingChange ? sharingSettings : undefined}
                    />
                </div>
            </div>
        </div>
    {/if}
{/snippet}

{#snippet liveRegion()}
    <div aria-live="polite" class="sr-only" role="status">{announcement}</div>
{/snippet}

{#if mode === 'page'}
    <section class="bg-background flex min-h-0 flex-1 flex-col overflow-hidden" data-assistant-page>
        <header class="flex items-center justify-between gap-3 border-b px-4 py-3">
            <div class="flex min-w-0 items-center gap-2.5">
                <ExieAvatar />
                <div class="min-w-0">
                    <h2 class="text-base leading-tight font-semibold">Exie</h2>
                    <p class="text-muted-foreground truncate text-xs">Your Exceptionless assistant</p>
                </div>
            </div>
            {@render headerActions()}
        </header>
        {@render conversation()}
        {@render composerArea()}
        {@render liveRegion()}
    </section>
{:else}
    <Sheet.Root bind:open>
        <Sheet.Content
            class="bg-background top-16! bottom-0! h-auto! w-full gap-0 sm:max-w-120!"
            data-assistant-panel
            data-tour="exie-panel"
            onInteractOutside={handleInteractOutside}
            onOpenAutoFocus={handleOpenAutoFocus}
            overlayProps={{
                class: 'top-16! bg-black/5 dark:bg-black/30 supports-backdrop-filter:backdrop-blur-[0.5px]'
            }}
            preventScroll={false}
        >
            <Sheet.Header class="flex-row items-center justify-between gap-2 border-b px-4 py-3 pr-12">
                <div class="flex min-w-0 items-center gap-2.5">
                    <ExieAvatar />
                    <div class="min-w-0">
                        <Sheet.Title class="leading-tight" level={2}>Exie</Sheet.Title>
                        <Sheet.Description class="truncate text-xs">Your Exceptionless assistant</Sheet.Description>
                    </div>
                </div>
                {@render headerActions()}
            </Sheet.Header>
            {@render conversation()}
            {@render composerArea()}
            {@render liveRegion()}
        </Sheet.Content>
    </Sheet.Root>
{/if}
