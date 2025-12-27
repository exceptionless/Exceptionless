import type { NewWebHook as NewWebhookBase, WebHook as WebhookBase } from '$generated/api';

export interface NewWebhook extends NewWebhookBase {
    event_types: WebhookKnownEventTypes[];
}

export interface Webhook extends WebhookBase {
    event_types: WebhookKnownEventTypes[];
}

export type WebhookKnownEventTypes = 'CriticalError' | 'CriticalEvent' | 'NewError' | 'NewEvent' | 'StackPromoted' | 'StackRegression';
