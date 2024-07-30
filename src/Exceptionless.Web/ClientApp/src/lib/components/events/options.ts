import type { PersistentEventKnownTypes } from '$lib/models/api';

import { StackStatus } from '$lib/models/api';

export const eventTypes: { label: string; value: PersistentEventKnownTypes }[] = [
    {
        label: 'Not Found',
        value: '404'
    },
    {
        label: 'Errors',
        value: 'error'
    },
    {
        label: 'Logs',
        value: 'log'
    },
    {
        label: 'Feature Usage',
        value: 'usage'
    },
    {
        label: 'Session Start',
        value: 'session'
    },
    {
        label: 'Session Heartbeat',
        value: 'heartbeat'
    },
    {
        label: 'Session End',
        value: 'sessionend'
    }
];

export const stackStatuses: { label: string; value: StackStatus }[] = [
    {
        label: 'Open',
        value: StackStatus.Open
    },
    {
        label: 'Fixed',
        value: StackStatus.Fixed
    },
    {
        label: 'Regressed',
        value: StackStatus.Regressed
    },
    {
        label: 'Snoozed',
        value: StackStatus.Snoozed
    },
    {
        label: 'Ignored',
        value: StackStatus.Ignored
    },
    {
        label: 'Discarded',
        value: StackStatus.Discarded
    }
];
