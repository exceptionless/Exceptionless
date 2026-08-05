import type { ViewProject } from '$features/projects/models';

import { describe, expect, it } from 'vitest';

import type { IPersistentEventData, PersistentEvent } from './models';

import { getErrorData, getExtendedDataItems } from './persistent-event';

function createEvent(data: IPersistentEventData): PersistentEvent {
    return {
        created_utc: '2026-06-04T00:00:00Z',
        data,
        date: '2026-06-04T00:00:00Z',
        id: '507f1f77bcf86cd799439011',
        is_first_occurrence: false,
        organization_id: '507f1f77bcf86cd799439012',
        project_id: '507f1f77bcf86cd799439013',
        stack_id: '507f1f77bcf86cd799439014'
    };
}

describe('getErrorData', () => {
    it('decodes JSON-encoded exception extended data before rendering it', () => {
        const event = createEvent({
            '@error': {
                data: {
                    '@ext': '{"ErrorCode":-2147467259,"ObjectName":"System.Net.Sockets.NetworkStream"}'
                },
                message: 'The operation failed.',
                type: 'System.Runtime.InteropServices.COMException'
            }
        });

        expect(getErrorData(event)).toEqual([
            {
                data: {
                    ErrorCode: -2147467259,
                    ObjectName: 'System.Net.Sockets.NetworkStream'
                },
                message: 'The operation failed.',
                type: 'System.Runtime.InteropServices.COMException'
            }
        ]);
    });

    it('preserves structured extended data and regular exception data', () => {
        const event = createEvent({
            '@simple_error': {
                data: {
                    '@ext': {
                        ErrorCode: 42
                    },
                    source: 'worker'
                },
                message: 'The operation failed.',
                type: 'TestException'
            }
        });

        expect(getErrorData(event)[0]?.data).toEqual({
            ErrorCode: 42,
            source: 'worker'
        });
    });

    it('preserves malformed encoded extended data without enumerating its characters', () => {
        const event = createEvent({
            '@error': {
                data: {
                    '@ext': '{invalid'
                },
                type: 'TestException'
            }
        });

        expect(getErrorData(event)[0]?.data).toEqual({
            '@ext': '{invalid'
        });
    });
});

describe('getExtendedDataItems', () => {
    it('orders promoted extended data by the project promoted tab order', () => {
        // Arrange
        const event = createEvent({
            alpha: 'Alpha',
            beta: 'Beta',
            gamma: 'Gamma'
        });
        const project = {
            promoted_tabs: ['gamma', 'alpha']
        } as ViewProject;

        // Act
        const items = getExtendedDataItems(event, project);

        // Assert
        expect(items.map((item) => item.title)).toEqual(['gamma', 'alpha', 'beta']);
        expect(items.map((item) => item.promoted)).toEqual([true, true, false]);
    });
});
