import { getContext, setContext } from 'svelte';

interface ProductTourControls {
    getNavigationTarget: () => HTMLElement | undefined;
    openCatalog: () => void;
}

const PRODUCT_TOUR_CONTROLS_CONTEXT_KEY = Symbol.for('exceptionless-product-tour-controls');

export function setProductTourControls(controls: ProductTourControls): void {
    setContext(PRODUCT_TOUR_CONTROLS_CONTEXT_KEY, controls);
}

export function tryUseProductTourControls(): ProductTourControls | undefined {
    return getContext<ProductTourControls | undefined>(PRODUCT_TOUR_CONTROLS_CONTEXT_KEY);
}
