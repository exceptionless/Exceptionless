import { useEventListener } from 'runed';

export class DocumentVisibility {
    #effectRegistered = false;
    #visible: boolean = $state(!document.hidden);

    get visible(): boolean | undefined {
        if ($effect.tracking() && !this.#effectRegistered) {
            this.#visible = !document.hidden;

            // If we are in an effect and this effect has not been registered yet
            // we match the current value, register the listener and return match
            $effect(() => {
                this.#effectRegistered = true;

                useEventListener(
                    () => document,
                    'visibilitychange',
                    () => (this.#visible = !document.hidden)
                );

                return () => (this.#effectRegistered = false);
            });
        } else if (!$effect.tracking()) {
            // Otherwise, just match media to get the current value
            this.#visible = !document.hidden;
        }

        return this.#visible;
    }
}
