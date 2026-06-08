const applePlatformPattern = /Mac|iPhone|iPad|iPod/i;

export type KeyboardShortcut = {
    key: string;
    keys: readonly ShortcutKey[];
};

export type ShortcutKey = 'Alt' | 'Mod' | 'Shift' | string;

export const appKeyboardShortcuts = {
    allEvents: { key: 'e', keys: ['E'] },
    commandPalette: { key: '/', keys: ['/'] },
    keyboardShortcuts: { key: '?', keys: ['?'] },
    stacks: { key: 'i', keys: ['I'] },
    switchOrganization: { key: 'o', keys: ['O'] },
    userMenu: { key: 'u', keys: ['U'] }
} as const satisfies Record<string, KeyboardShortcut>;

export function formatKeyboardShortcut(keys: readonly ShortcutKey[]): string {
    return formatKeyboardShortcutForPlatform(keys, isApplePlatform());
}

export function formatKeyboardShortcutForPlatform(keys: readonly ShortcutKey[], isApplePlatformValue: boolean): string {
    const formattedKeys = keys.map((key) => formatShortcutKey(key, isApplePlatformValue));

    return formattedKeys.join('');
}

export function isKeyboardShortcut(event: KeyboardEvent, shortcut: KeyboardShortcut): boolean {
    return event.key.toLowerCase() === shortcut.key;
}

function formatShortcutKey(key: ShortcutKey, isApplePlatformValue: boolean): string {
    switch (key) {
        case 'Alt':
            return isApplePlatformValue ? '⌥' : '⎇';
        case 'Mod':
            return isApplePlatformValue ? '⌘' : '⌃';
        case 'Shift':
            return '⇧';
        default:
            return key;
    }
}

function isApplePlatform(): boolean {
    if (typeof navigator === 'undefined') {
        return false;
    }

    return applePlatformPattern.test(navigator.platform) || applePlatformPattern.test(navigator.userAgent);
}
