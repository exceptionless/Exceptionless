import type { DropdownItem } from '$features/shared/options';

import type { WebhookKnownEventTypes } from './models';

export const webhookEventTypes: DropdownItem<WebhookKnownEventTypes>[] = [
    {
        description: 'Occurs when a new error that has never been seen before is reported to your project.',
        label: 'New Error',
        value: 'NewError'
    },
    {
        description: 'Occurs when an error that has been marked as critical is reported to your project.',
        label: 'Critical Error',
        value: 'CriticalError'
    },
    {
        description: 'Occurs when an event that has been marked as fixed has reoccurred in your project.',
        label: 'Regression',
        value: 'StackRegression'
    },
    {
        description: 'Occurs when a new event that has never been seen before is reported to your project.',
        label: 'New Event',
        value: 'NewEvent'
    },
    {
        description: 'Occurs when an event that has been marked as critical is reported to your project.',
        label: 'Critical Event',
        value: 'CriticalEvent'
    },
    {
        description: 'Used to promote event stacks to external systems.',
        label: 'Promoted',
        value: 'StackPromoted'
    }
];
