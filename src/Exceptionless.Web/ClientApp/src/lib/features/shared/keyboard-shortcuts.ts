const applePlatformPattern = /Mac|iPhone|iPad|iPod/i;

type ShortcutKey = 'Alt' | 'Mod' | 'Shift' | string;

export function formatKeyboardShortcut(keys: ShortcutKey[]): string {
    return formatKeyboardShortcutForPlatform(keys, isApplePlatform());
}

export function formatKeyboardShortcutForPlatform(keys: ShortcutKey[], isApplePlatformValue: boolean): string {
    const formattedKeys = keys.map((key) => formatShortcutKey(key, isApplePlatformValue));

    return formattedKeys.join('');
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
