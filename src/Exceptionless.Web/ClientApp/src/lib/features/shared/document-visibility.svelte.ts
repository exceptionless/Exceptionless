import { useEventListener } from 'runed';

export class DocumentVisibility {
    get visible(): boolean {
        return this.#visible;
    }

    #visible = $state(!document.hidden);

    constructor() {
        useEventListener(
            () => document,
            'visibilitychange',
            () => (this.#visible = !document.hidden)
        );
    }
}
