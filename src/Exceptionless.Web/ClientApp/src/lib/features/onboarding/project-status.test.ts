import { describe, expect, it } from 'vitest';

import { getContinueSetupHref, isProjectAwaitingFirstEvent } from './project-status';

describe('project onboarding status', () => {
    it.each([
        [false, true],
        [true, false],
        [null, false]
    ])('treats is_configured %s as awaiting the first event: %s', (isConfigured, expected) => {
        // Arrange
        const project = { is_configured: isConfigured };

        // Act
        const result = isProjectAwaitingFirstEvent(project);

        // Assert
        expect(result).toBe(expected);
    });

    it('continues setup with the first quiet project', () => {
        // Arrange
        const projects = [
            { id: 'configured', is_configured: true },
            { id: '5f0000000000000000000001', is_configured: false }
        ];

        // Act
        const href = getContinueSetupHref(projects);

        // Assert
        expect(href).toBe('/project/5f0000000000000000000001/configure');
    });

    it.each([[[{ id: 'configured', is_configured: true }]], [[]]])('adds a project when none is waiting: %j', (projects) => {
        // Act
        const href = getContinueSetupHref(projects);

        // Assert
        expect(href).toBe('/project/add');
    });
});
