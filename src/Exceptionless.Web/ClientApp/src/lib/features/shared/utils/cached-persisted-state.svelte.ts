import { PersistedState } from 'runed';
import { untrack } from 'svelte';

/**
 * Wraps PersistedState to cache reads and prevent excessive reactive signal triggers.
 *
 * Problem: PersistedState reads from localStorage and calls deserialize on every `.current` access,
 * which triggers reactive subscriptions. When dozens of queries check `enabled: () => !!state.current`,
 * this causes a cascading reactivity loop.
 *
 * Solution: Cache the value in a local $state. Reads come from the cache (instant, no deserialize).
 * Writes update both the cache and PersistedState. PersistedState handles cross-tab sync internally.
 */
export class CachedPersistedState<T> {
    /**
     * Get the cached value without triggering PersistedState's deserialize
     */
    get current(): T {
        return this.#cached;
    }
    /**
     * Set the value, updating both the cache and PersistedState
     */
    set current(newValue: T) {
        this.#cached = newValue;
        this.#persisted.current = newValue;
    }

    #cached = $state<T>(null!);

    #persisted: PersistedState<T>;

    constructor(key: string, initialValue: T, options?: object) {
        // Initialize PersistedState from storage
        this.#persisted = new PersistedState(key, initialValue, options);

        // Cache the initial value (untrack to avoid creating dependencies during construction)
        this.#cached = untrack(() => this.#persisted.current);
    }
}
