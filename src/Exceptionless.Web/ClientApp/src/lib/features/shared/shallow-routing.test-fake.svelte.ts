import type { AfterNavigate, BeforeNavigate, GotoOptions, NavigationType } from '$app/navigation';

import { flushSync, onDestroy, tick } from 'svelte';
import { SvelteURL } from 'svelte/reactivity';
import { vi } from 'vitest';

import { trackHistoryTraversals } from './history-traversal';

/**
 * A stand-in for SvelteKit 3 shallow routing in unit tests. A shallow `goto` runs the
 * navigation hooks with `shallow: true`, writes the history entry, and then updates
 * `page.state`. Back and Forward restore the URL first and apply the entry's page state in a
 * popstate listener, as SvelteKit does.
 *
 * Mock `$app/navigation` and `$app/state` with the exports of `shallowRouting`.
 */
const pageStateKey = 'testPageState';
const afterNavigateCallbacks = new Set<(navigation: AfterNavigate) => void>();
const beforeNavigateCallbacks = new Set<(navigation: BeforeNavigate) => void>();

class FakePage {
    public shallow = $state.raw<null | { url: URL }>(null);
    public state = $state.raw<App.PageState>({});
    public url = $state.raw<URL>(new SvelteURL('http://localhost/'));
}

const page = new FakePage();
const navigating = {
    complete: null as null | Promise<void>
};

function getHistoryEntryState(historyState: unknown): App.PageState {
    return (historyState as null | Record<string, App.PageState>)?.[pageStateKey] ?? {};
}

function setShallowUrl(): void {
    page.shallow =
        window.location.href === page.url.href
            ? null
            : {
                  url: new SvelteURL(window.location.href)
              };
}

// As in the app's client `init` hook, count traversals before the router starts listening.
trackHistoryTraversals();

// Like SvelteKit's router, apply the destination's page state in a popstate listener that is
// registered before any component's. Browsers run a microtask checkpoint after each listener,
// so effects that track `page.state` run before later listeners do; `flushSync` models that.
window.addEventListener('popstate', (event) => {
    page.state = getHistoryEntryState(event.state);
    setShallowUrl();
    flushSync();
});

function createNavigation(type: NavigationType, to: URL, shallow: boolean, willUnload = false) {
    return {
        complete: Promise.resolve(),
        delta: undefined,
        event: undefined,
        from: {
            params: {},
            route: {
                id: null
            },
            scroll: null,
            url: page.url
        },
        shallow,
        to: {
            params: {},
            route: {
                id: null
            },
            scroll: null,
            url: to
        },
        type,
        willUnload
    };
}

function register<T>(callbacks: Set<T>, callback: T): void {
    callbacks.add(callback);
    onDestroy(() => callbacks.delete(callback));
}

export const shallowRouting = {
    afterNavigate: vi.fn((callback: (navigation: AfterNavigate) => void) => register(afterNavigateCallbacks, callback)),

    /** Runs the `afterNavigate` hooks for a navigation that rendered the current page. */
    afterNavigating(type: Exclude<NavigationType, 'leave'> = 'link'): void {
        const navigation = createNavigation(type, page.url, false);
        for (const callback of afterNavigateCallbacks) {
            callback(navigation as AfterNavigate);
        }
    },

    /** Runs the `beforeNavigate` hooks for a navigation away from the page and returns its `cancel` spy. */
    beforeLeaving(type: Exclude<NavigationType, 'enter'>, to = '/elsewhere', willUnload = type === 'leave'): ReturnType<typeof vi.fn> {
        const cancel = vi.fn();
        const navigation = createNavigation(type, new SvelteURL(to, window.location.href), false, willUnload);
        for (const callback of beforeNavigateCallbacks) {
            callback({
                ...navigation,
                cancel
            } as BeforeNavigate);
        }

        return cancel;
    },

    beforeNavigate: vi.fn((callback: (navigation: BeforeNavigate) => void) => register(beforeNavigateCallbacks, callback)),

    /** Applies pending shallow writes and the reactive updates that follow them. */
    async flush(): Promise<void> {
        for (let index = 0; index < 10; index++) {
            await Promise.resolve();
        }

        await tick();
    },

    goto: vi.fn(async (url: string | URL, { replace = false, shallow = false, state = {} }: GotoOptions = {}) => {
        const target = new SvelteURL(url, window.location.href);
        if (!shallow) {
            return;
        }

        const navigation = createNavigation('goto', target, true);
        let cancelled = false;
        for (const callback of beforeNavigateCallbacks) {
            callback({
                ...navigation,
                cancel: () => (cancelled = true)
            } as BeforeNavigate);
        }

        if (cancelled) {
            return;
        }

        if (replace) {
            window.history.replaceState(
                {
                    [pageStateKey]: state
                },
                '',
                target
            );
        } else {
            window.history.pushState(
                {
                    [pageStateKey]: state
                },
                '',
                target
            );
        }

        await Promise.resolve();
        page.state = state;
        page.shallow = {
            url: target
        };
        for (const callback of afterNavigateCallbacks) {
            callback(navigation as AfterNavigate);
        }
    }),

    /** Returns the page state stored with the current history entry. */
    historyEntryState(): App.PageState | undefined {
        return (window.history.state as null | Record<string, App.PageState>)?.[pageStateKey];
    },

    navigating,

    page,

    /** Starts a test on a rendered page at `url` whose history entry has `state`. */
    reset(url = '/', state: App.PageState = {}): void {
        shallowRouting.goto.mockClear();
        shallowRouting.visit(url, state);
    },

    /** Marks a navigation as in progress and returns a function that finishes it. */
    startNavigation(): () => Promise<void> {
        const { promise, resolve } = Promise.withResolvers<void>();
        navigating.complete = promise;
        return async () => {
            navigating.complete = null;
            resolve();
            await shallowRouting.flush();
        };
    },

    /** Simulates Back or Forward to an entry with `url` and its stored page state. */
    async traverse(url: string, state: App.PageState = {}): Promise<void> {
        window.history.replaceState(
            {
                [pageStateKey]: state
            },
            '',
            url
        );
        window.dispatchEvent(
            new PopStateEvent('popstate', {
                state: window.history.state
            })
        );
        await shallowRouting.flush();
    },

    /** Renders the page for an entry with `url` and `state`, as a navigation or reload does. */
    visit(url: string, state: App.PageState = {}): void {
        window.history.replaceState(
            {
                [pageStateKey]: state
            },
            '',
            url
        );
        page.state = state;
        page.url = new SvelteURL(window.location.href);
        setShallowUrl();
        navigating.complete = null;
    }
};
