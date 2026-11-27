const STORAGE_KEY_PREFIX = 'getting-started-checklist-expanded';

/**
 * Remembers whether each user expanded or collapsed the getting-started checklist on this device.
 * `expanded` is undefined until the user chooses, so the checklist can pick a sensible default.
 */
export class ChecklistLayoutPreference {
    public get expanded(): boolean | undefined {
        // Reading `version` makes callers react to `set`; the cache itself is not reactive.
        void this.version;
        const userId = this.getUserId();
        if (!userId) {
            return undefined;
        }

        if (!(userId in this.cache)) {
            this.cache[userId] = readPreference(userId);
        }

        return this.cache[userId];
    }

    // Plain object on purpose: filling it while a derived value reads `expanded` must not be a reactive write.
    private readonly cache: Record<string, boolean | undefined> = {};
    private version = $state(0);

    constructor(private readonly getUserId: () => string | undefined) {}

    public set(expanded: boolean): void {
        const userId = this.getUserId();
        if (!userId) {
            return;
        }

        this.cache[userId] = expanded;
        try {
            localStorage.setItem(getChecklistLayoutStorageKey(userId), String(expanded));
        } catch {
            // Storage can be unavailable (private mode); the choice still applies for this visit.
        }

        this.version += 1;
    }
}

export function getChecklistLayoutStorageKey(userId: string): string {
    return `${STORAGE_KEY_PREFIX}:${userId}`;
}

function readPreference(userId: string): boolean | undefined {
    try {
        const value = localStorage.getItem(getChecklistLayoutStorageKey(userId));
        return value === null ? undefined : value === 'true';
    } catch {
        return undefined;
    }
}
