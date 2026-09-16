import { useEventListener } from 'runed';

export class DocumentVisibility {
    public get visible(): boolean {
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
