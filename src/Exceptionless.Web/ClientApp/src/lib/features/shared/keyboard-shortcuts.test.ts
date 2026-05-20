import { describe, expect, it } from 'vitest';

import { formatKeyboardShortcutForPlatform } from './keyboard-shortcuts';

describe('formatKeyboardShortcutForPlatform', () => {
    it('should format modifier shortcuts for Apple platforms', () => {
        expect(formatKeyboardShortcutForPlatform(['Mod', 'K'], true)).toBe('⌘K');
        expect(formatKeyboardShortcutForPlatform(['Shift', 'Mod', 'Q'], true)).toBe('⇧⌘Q');
        expect(formatKeyboardShortcutForPlatform(['Alt'], true)).toBe('⌥');
    });

    it('should format modifier shortcuts for Windows and Linux platforms', () => {
        expect(formatKeyboardShortcutForPlatform(['Mod', 'K'], false)).toBe('⌃K');
        expect(formatKeyboardShortcutForPlatform(['Shift', 'Mod', 'Q'], false)).toBe('⇧⌃Q');
        expect(formatKeyboardShortcutForPlatform(['Alt'], false)).toBe('⎇');
    });
});
