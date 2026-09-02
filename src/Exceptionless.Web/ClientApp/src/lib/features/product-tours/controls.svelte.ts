import { getContext, setContext } from 'svelte';

interface ProductTourControls {
    closeOverlays: () => void;
    getGuidedToursTarget: () => HTMLElement | undefined;
    openCatalog: () => void;
    showGuidedToursMenu: () => Promise<void>;
}

const PRODUCT_TOUR_CONTROLS_CONTEXT_KEY = Symbol.for('exceptionless-product-tour-controls');

export function setProductTourControls(controls: ProductTourControls): void {
    setContext(PRODUCT_TOUR_CONTROLS_CONTEXT_KEY, controls);
}

export function tryUseProductTourControls(): ProductTourControls | undefined {
    return getContext<ProductTourControls | undefined>(PRODUCT_TOUR_CONTROLS_CONTEXT_KEY);
}
