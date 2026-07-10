import type { ViewProject } from '$features/projects/models';

import { describe, expect, it } from 'vitest';

import type { PersistentEvent } from './models';

import { getExtendedDataItems } from './persistent-event';

describe('getExtendedDataItems', () => {
    it('orders promoted extended data by the project promoted tab order', () => {
        // Arrange
        const event = {
            created_utc: '2026-06-04T00:00:00Z',
            data: {
                alpha: 'Alpha',
                beta: 'Beta',
                gamma: 'Gamma'
            },
            date: '2026-06-04T00:00:00Z',
            id: '507f1f77bcf86cd799439011',
            is_first_occurrence: false,
            organization_id: '507f1f77bcf86cd799439012',
            project_id: '507f1f77bcf86cd799439013',
            stack_id: '507f1f77bcf86cd799439014'
        } as PersistentEvent;
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
