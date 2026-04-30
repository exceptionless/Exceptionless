/**
 * Reactive state for pages to declare themselves as requiring premium features.
 * Pages set this on mount; the layout reads it to show the premium notification.
 */
class PremiumPageState {
    get featureName() {
        return this._featureName;
    }

    get requiresPremium() {
        return this._featureName !== undefined;
    }

    private _featureName = $state<string | undefined>(undefined);

    reset() {
        this._featureName = undefined;
    }

    set(featureName: string) {
        this._featureName = featureName;
    }
}

export const premiumPage = new PremiumPageState();
