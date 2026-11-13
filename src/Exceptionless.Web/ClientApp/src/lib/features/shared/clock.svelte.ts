import { on } from 'svelte/events';
import { createSubscriber } from 'svelte/reactivity';

/** How often the clock updates while something reads it. */
export const CLOCK_TICK_MS = 60 * 1000;

/**
 * Shared reactive time for values that change as time passes, such as trial days left or a notice dismissed until
 * tomorrow. Reading `clock.now` in a `$derived` or template re-runs it each minute and when the tab becomes visible
 * again. Every reader shares one timer, and it runs only while something is reading the clock.
 */
class Clock {
    public get now(): Date {
        this.#subscribe();
        return new Date();
    }

    readonly #subscribe = createSubscriber((update) => {
        const interval = setInterval(update, CLOCK_TICK_MS);
        const removeVisibilityListener = on(document, 'visibilitychange', () => {
            if (!document.hidden) {
                update();
            }
        });

        return () => {
            clearInterval(interval);
            removeVisibilityListener();
        };
    });
}

export const clock = new Clock();
