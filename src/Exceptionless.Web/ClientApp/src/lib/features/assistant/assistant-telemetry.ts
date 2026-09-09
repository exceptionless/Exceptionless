import { submitFeatureUsage, submitLog } from '$features/auth/exceptionless-session';

import type { AssistantStreamEvent } from './assistant-stream';

import { assistantToolResultFailed } from './assistant-tool-result';

const maximumMessageCharacters = 16_384;

export type AssistantPromptSource = 'composer' | 'queued' | 'regenerate' | 'retry' | 'starter' | 'suggested_action';

export type AssistantStopReason = 'access_changed' | 'component_unmounted' | 'conversation_cleared' | 'organization_changed' | 'user_stopped';
export interface AssistantTelemetryContext {
    assistant_message_id?: string;
    conversation_id: string;
    mode: 'page' | 'sheet';
    organization_id?: string;
    path?: string;
    project_id?: string;
    user_message_id?: string;
}
export type AssistantTurnOutcome = 'cancelled' | 'completed' | 'failed';

export class AssistantTurnTelemetry {
    private content = '';
    private contentCharacters = 0;
    private errorMessage: string | undefined;
    private failureReason: string | undefined;
    private finished = false;
    private firstTextDuration: number | undefined;
    private receivedDone = false;
    private started = performance.now();
    private toolCalls = 0;
    private toolFailures = 0;

    constructor(
        readonly context: AssistantTelemetryContext,
        prompt: string,
        source: AssistantPromptSource,
        details: Record<string, unknown> = {}
    ) {
        trackAssistantEvent('assistant.MessageSent', context, { ...details, prompt_source: source, role: 'user' }, prompt);
    }

    fail(message: string, reason: string): void {
        this.errorMessage ??= message.slice(0, 2048);
        this.failureReason ??= reason;
    }

    finish(stopReason?: AssistantStopReason, details: Record<string, unknown> = {}): AssistantTurnOutcome | undefined {
        if (this.finished) {
            return;
        }
        this.finished = true;
        const outcome = stopReason ? 'cancelled' : this.failureReason || !this.receivedDone || this.contentCharacters === 0 ? 'failed' : 'completed';
        const reason =
            stopReason ?? this.failureReason ?? (!this.receivedDone ? 'incomplete_stream' : this.contentCharacters === 0 ? 'empty_response' : undefined);
        const feature = { cancelled: 'assistant.ResponseCancelled', completed: 'assistant.ResponseCompleted', failed: 'assistant.ResponseFailed' }[outcome];
        trackAssistantEvent(
            feature,
            this.context,
            {
                ...details,
                duration_ms: Math.round(performance.now() - this.started),
                error_message: this.errorMessage,
                first_text_duration_ms: this.firstTextDuration,
                message_characters: this.contentCharacters || this.errorMessage?.length || 0,
                message_truncated: this.contentCharacters > maximumMessageCharacters,
                outcome,
                reason,
                received_done: this.receivedDone,
                response_characters: this.contentCharacters,
                response_truncated: this.contentCharacters > maximumMessageCharacters,
                role: 'assistant',
                tool_calls: this.toolCalls,
                tool_failures: this.toolFailures
            },
            this.content || this.errorMessage || ''
        );
        return outcome;
    }

    observe(event: AssistantStreamEvent): void {
        if (this.finished) {
            return;
        }
        if (event.type === 'text_delta' && event.text) {
            this.contentCharacters += event.text.length;
            this.content += event.text.slice(0, Math.max(0, maximumMessageCharacters - this.content.length));
            this.firstTextDuration ??= Math.round(performance.now() - this.started);
        } else if (event.type === 'tool_call') {
            this.toolCalls++;
        } else if (event.type === 'tool_result' && assistantToolResultFailed(event.result)) {
            this.toolFailures++;
        } else if (event.type === 'error') {
            this.fail(event.message ?? 'Exie could not complete this request.', 'stream_error');
        } else if (event.type === 'done') {
            this.receivedDone = true;
        }
    }
}

export function trackAssistantEvent(feature: string, context: AssistantTelemetryContext, details: Record<string, unknown> = {}, message?: string): void {
    const properties = {
        exie: {
            ...context,
            ...(message !== undefined && { message_characters: message.length, message_truncated: message.length > maximumMessageCharacters }),
            ...details,
            schema_version: 1
        }
    };
    // Session/user identity, queueing, and filtering come from the existing SDK.
    // A telemetry failure must not interrupt a chat or generate another telemetry event.
    const submission =
        message === undefined ? submitFeatureUsage(feature, properties) : submitLog(feature, message.slice(0, maximumMessageCharacters), properties);
    void submission.catch(() => {});
}
