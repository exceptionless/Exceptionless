import { describe, expect, it } from 'vitest';

import { appKeyboardShortcuts, formatKeyboardShortcutForPlatform, isKeyboardShortcut } from './keyboard-shortcuts';

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

    it('should format single key shortcuts', () => {
        expect(formatKeyboardShortcutForPlatform(appKeyboardShortcuts.switchOrganization.keys, false)).toBe('O');
        expect(formatKeyboardShortcutForPlatform(appKeyboardShortcuts.userMenu.keys, false)).toBe('U');
        expect(formatKeyboardShortcutForPlatform(appKeyboardShortcuts.keyboardShortcuts.keys, false)).toBe('?');
    });

    it('should match shortcut keys case-insensitively', () => {
        expect(isKeyboardShortcut({ key: 'o' } as KeyboardEvent, appKeyboardShortcuts.switchOrganization)).toBe(true);
        expect(isKeyboardShortcut({ key: 'O' } as KeyboardEvent, appKeyboardShortcuts.switchOrganization)).toBe(true);
        expect(isKeyboardShortcut({ key: 'p' } as KeyboardEvent, appKeyboardShortcuts.switchOrganization)).toBe(false);
        expect(isKeyboardShortcut({ key: 'u' } as KeyboardEvent, appKeyboardShortcuts.userMenu)).toBe(true);
        expect(isKeyboardShortcut({ key: 'U' } as KeyboardEvent, appKeyboardShortcuts.userMenu)).toBe(true);
        expect(isKeyboardShortcut({ key: '?' } as KeyboardEvent, appKeyboardShortcuts.keyboardShortcuts)).toBe(true);
    });
});
