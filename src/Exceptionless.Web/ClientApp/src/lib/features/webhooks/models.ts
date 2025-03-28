import { NewWebHook as NewWebhookBase, WebHook as WebhookBase } from '$generated/api';
import { ArrayNotEmpty, IsDefined } from 'class-validator';

export type WebhookKnownEventTypes = 'CriticalError' | 'CriticalEvent' | 'NewError' | 'NewEvent' | 'StackPromoted' | 'StackRegression';

export class NewWebhook extends NewWebhookBase {
    @ArrayNotEmpty({ message: 'Event Types should not be empty.' })
    @IsDefined({ message: 'Event Types is required.' })
    override event_types: WebhookKnownEventTypes[] = [];
}

export class Webhook extends WebhookBase {
    @ArrayNotEmpty({ message: 'Event Types should not be empty.' })
    @IsDefined({ message: 'Event Types is required.' })
    override event_types: WebhookKnownEventTypes[] = [];
}
