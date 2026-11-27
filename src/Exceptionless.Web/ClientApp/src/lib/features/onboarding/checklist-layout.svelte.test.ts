import { flushSync } from 'svelte';
import { beforeEach, describe, expect, it } from 'vitest';

import { ChecklistLayoutPreference, getChecklistLayoutStorageKey } from './checklist-layout.svelte';

describe('ChecklistLayoutPreference', () => {
    beforeEach(() => localStorage.clear());

    it('has no preference until the user chooses, then remembers it per user', () => {
        // Arrange
        let userId: string | undefined = 'user-a';
        const preference = new ChecklistLayoutPreference(() => userId);

        // Act
        const initial = preference.expanded;
        preference.set(true);

        // Assert
        expect(initial).toBeUndefined();
        expect(preference.expanded).toBe(true);
        expect(localStorage.getItem(getChecklistLayoutStorageKey('user-a'))).toBe('true');
        userId = 'user-b';
        expect(preference.expanded).toBeUndefined();
        expect(new ChecklistLayoutPreference(() => 'user-a').expanded).toBe(true);
    });

    it('updates reactive readers when the choice changes', () => {
        // Arrange
        const preference = new ChecklistLayoutPreference(() => 'user-a');
        let observed: boolean | undefined;
        const cleanup = $effect.root(() => {
            $effect(() => {
                observed = preference.expanded;
            });
        });
        flushSync();

        // Act
        preference.set(false);
        flushSync();

        // Assert
        expect(observed).toBe(false);
        cleanup();
    });
});
