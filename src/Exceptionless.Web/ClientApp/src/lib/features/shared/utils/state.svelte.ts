/**
 * Safely clone state data to prevent cache mutation and reactive entanglement.
 *
 * This utility combines `$state.snapshot()` for reactive safety with `structuredClone()`
 * for deep cloning to ensure form data is completely independent from cached/reactive state.
 *
 * @param state - The state object to clone safely
 * @returns A deep, non-reactive clone of the state object, or undefined if state is falsy
 *
 * @example
 * ```svelte
 * // Form initialization
 * const form = superForm(defaults(structuredCloneState(settings) || new NotificationSettings(), classvalidatorClient(NotificationSettings)), {
 *     // form options...
 * });
 *
 * // Form reset
 * const clonedSettings = structuredCloneState(settings);
 * form.reset({ data: clonedSettings, keepMessage: true });
 * ```
 */
export function structuredCloneState<T>(state: T): T | undefined {
    if (!state) {
        return state;
    }

    return structuredClone($state.snapshot(state)) as T;
}
