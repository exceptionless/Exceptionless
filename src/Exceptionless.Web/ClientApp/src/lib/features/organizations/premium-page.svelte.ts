/**
 * Reactive state for pages to declare themselves as requiring premium features.
 * Pages set `premiumPage.current` on mount; the layout reads it to show the premium notification.
 * Follows the same getter/setter pattern as CachedPersistedState.
 */
class PremiumPageState {
    get current(): string | undefined {
        return this.#value;
    }

    set current(featureName: string | undefined) {
        this.#value = featureName;
    }

    get requiresPremium() {
        return this.#value !== undefined;
    }

    #value = $state<string | undefined>(undefined);
}

export const premiumPage = new PremiumPageState();
