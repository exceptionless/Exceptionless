import { StackStatus } from '$features/stacks/models';

import type { LogLevel } from '../models/event-data';
import type { PersistentEventKnownTypes } from '../models/index';

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

export const logLevels: { label: string; value: LogLevel }[] = [
    {
        label: 'Trace',
        value: 'trace'
    },
    {
        label: 'Debug',
        value: 'debug'
    },
    {
        label: 'Information',
        value: 'info'
    },
    {
        label: 'Warning',
        value: 'warn'
    },
    {
        label: 'Error',
        value: 'error'
    },
    {
        label: 'Fatal',
        value: 'fatal'
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
