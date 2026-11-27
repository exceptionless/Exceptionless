import { goto } from '$app/navigation';
import { toast } from 'svelte-sonner';

export interface FirstEventToastOptions {
    /** Where "View events" goes; leave it out when the user is already looking at events. */
    getEventsHref: () => string | undefined;
    getOrganizationId: () => string | undefined;
    hasReceivedEvents: () => boolean;
    isAwaitingFirstEvent: () => boolean;
    /** True while the user is on a page that shows Exie's empty state. */
    isOnEmptyStatePage: () => boolean;
}

/**
 * Celebrates the first event when it arrives while Exie's empty state is on screen, because the table
 * silently replaces it otherwise. Only a transition seen on the page counts, so loading a page that
 * already has events, switching organizations, or coming back later never shows the toast.
 */
export function useFirstEventToast(options: FirstEventToastOptions): void {
    let awaitingOrganizationId: string | undefined;

    $effect(() => {
        const organizationId = options.getOrganizationId();
        if (!options.isOnEmptyStatePage() || !organizationId) {
            awaitingOrganizationId = undefined;
            return;
        }

        if (options.isAwaitingFirstEvent()) {
            awaitingOrganizationId = organizationId;
            return;
        }

        const wasAwaiting = awaitingOrganizationId === organizationId;
        awaitingOrganizationId = undefined;
        if (wasAwaiting && options.hasReceivedEvents()) {
            const eventsHref = options.getEventsHref();
            toast.success('Your first event just landed!', {
                action: eventsHref
                    ? {
                          label: 'View events',
                          onClick: () => void goto(eventsHref)
                      }
                    : undefined,
                description: "I'll group repeats into stacks and let you know what's new."
            });
        }
    });
}
