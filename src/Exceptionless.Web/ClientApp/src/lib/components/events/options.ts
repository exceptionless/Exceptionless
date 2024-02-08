import type { PersistentEventKnownTypes } from '$lib/models/api';
import { StackStatus } from '$lib/models/api';

export const eventTypes: { value: PersistentEventKnownTypes; label: string }[] = [
    {
        value: '404',
        label: 'Not Found'
    },
    {
        value: 'error',
        label: 'Errors'
    },
    {
        value: 'log',
        label: 'Logs'
    },
    {
        value: 'usage',
        label: 'Feature Usage'
    },
    {
        value: 'session',
        label: 'Session Start'
    },
    {
        value: 'heartbeat',
        label: 'Session Heartbeat'
    },
    {
        value: 'sessionend',
        label: 'Session End'
    }
];

export const stackStatuses: { value: StackStatus; label: string }[] = [
    {
        value: StackStatus.Open,
        label: 'Open'
    },
    {
        value: StackStatus.Fixed,
        label: 'Fixed'
    },
    {
        value: StackStatus.Regressed,
        label: 'Regressed'
    },
    {
        value: StackStatus.Snoozed,
        label: 'Snoozed'
    },
    {
        value: StackStatus.Ignored,
        label: 'Ignored'
    },
    {
        value: StackStatus.Discarded,
        label: 'Discarded'
    }
];
