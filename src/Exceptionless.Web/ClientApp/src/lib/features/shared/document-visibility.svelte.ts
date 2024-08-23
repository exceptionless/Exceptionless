import { useEventListener } from 'runed';

export class DocumentVisibility {
    #effectRegistered = 0;
    #visible = $state<boolean | undefined>(!document.hidden);

    get visible(): boolean {
        if ($effect.tracking() && this.#effectRegistered === 0) {
            // If we are in an effect and this effect has not been registered yet
            // we match the current value, register the listener and return match
            $effect(() => {
                this.#effectRegistered++;

                useEventListener(
                    () => document,
                    'visibilitychange',
                    () => (this.#visible = !document.hidden)
                );

                return () => {
                    this.#effectRegistered--;
                    // if we deregister the event it means it's not used in any component
                    // and we want to go back to use the value from `this.#mediaQueryList.matches`
                    this.#visible = undefined;
                };
            });
        }

        return this.#visible ?? !document.hidden;
    }
}
