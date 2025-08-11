import type { DropdownItem } from '$features/shared/options';

import type { LogLevel } from './models/event-data';
import type { PersistentEventKnownTypes } from './models/index';

export const eventTypes: DropdownItem<PersistentEventKnownTypes>[] = [
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

export const logLevels: DropdownItem<LogLevel>[] = [
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
    },
    {
        label: 'Off',
        value: 'off'
    }
];
