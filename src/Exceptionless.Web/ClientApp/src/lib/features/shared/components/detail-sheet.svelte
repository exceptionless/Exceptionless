<script lang="ts">
    import type { Snippet } from 'svelte';

    import { browser } from '$app/environment';
    import { beforeNavigate, goto, pushState, replaceState } from '$app/navigation';
    import { page } from '$app/state';
    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import { onMount } from 'svelte';

    import { type DetailSheetHistoryEntry, detailSheetHistoryStateKey, type DetailSheetPageState } from '../history-state';
    import { preserveDetailSheetForAssistant } from './detail-sheet-interaction';

    interface Props {
        actions?: Snippet;
        children: Snippet;
        detailsHref: string;
        historyKey: string;
        historyValue: null | string | undefined;
        onClose: () => void;
        onOpen: (historyValue: string) => void;
        open: boolean;
        title: string;
    }

    const svelteKitPageStateKey = 'sveltekit:states';

    interface PendingNavigation {
        options?: LinkNavigationOptions;
        url: URL;
    }

    interface LinkNavigationOptions {
        keepFocus?: boolean;
        noScroll?: boolean;
        replaceState?: boolean;
    }

    let { actions, children, detailsHref, historyKey, historyValue, onClose, onOpen, open, title }: Props = $props();
    let historyEntryUrl: string | undefined;
    let historyEntryValue: string | undefined;
    let historyReady = $state(false);
    let ownsHistoryEntry = false;
    let parentCloseTimer: number | undefined;
    let pendingNavigation: PendingNavigation | undefined;
    let pendingNavigationTimer: number | undefined;
    let selectedLinkNavigationOptions: LinkNavigationOptions | undefined;
    let wasOpen = false;

    function getCurrentUrl(): string {
        return `${page.url.pathname}${page.url.search}${page.url.hash}`;
    }

    function getBrowserUrl(): string {
        return `${window.location.pathname}${window.location.search}${window.location.hash}`;
    }

    function getLinkNavigationOptions(link: HTMLAnchorElement): LinkNavigationOptions | undefined {
        function getOption(name: string): boolean | undefined {
            let element: Element | null = link;
            while (element && element !== document.documentElement) {
                const value = element.getAttribute(`data-sveltekit-${name}`);
                if (value !== null) {
                    return value === '' || value === 'true' ? true : value === 'off' || value === 'false' ? false : undefined;
                }

                element = element.parentElement;
            }

            return undefined;
        }

        const options: LinkNavigationOptions = {
            keepFocus: getOption('keepfocus'),
            noScroll: getOption('noscroll'),
            replaceState: getOption('replacestate')
        };

        return Object.values(options).some((value) => value !== undefined) ? options : undefined;
    }

    function clearOwnedHistoryEntry(): void {
        historyEntryUrl = undefined;
        historyEntryValue = undefined;
        ownsHistoryEntry = false;
    }

    function createHistoryState(value?: string): Record<string, unknown> {
        return {
            ...page.state,
            [detailSheetHistoryStateKey]: value ? { key: historyKey, value } : undefined
        };
    }

    function getHistoryEntry(historyState: unknown): DetailSheetHistoryEntry | undefined {
        if (!historyState || typeof historyState !== 'object') {
            return undefined;
        }

        const rawHistoryState = historyState as Record<string, unknown>;
        const pageState = (rawHistoryState[svelteKitPageStateKey] ?? rawHistoryState) as DetailSheetPageState;
        const entry = pageState[detailSheetHistoryStateKey];
        return entry?.key === historyKey && typeof entry.value === 'string' ? entry : undefined;
    }

    function restoreOwnedHistoryEntry(entry: DetailSheetHistoryEntry): void {
        historyEntryUrl = getCurrentUrl();
        historyEntryValue = entry.value;
        ownsHistoryEntry = true;
        wasOpen = true;

        if (!open || historyValue !== entry.value) {
            onOpen(entry.value);
        }
    }

    function consumeOwnedHistoryEntry(): void {
        if (!browser || !ownsHistoryEntry) {
            return;
        }

        const shouldTraverseBack = historyEntryUrl === getCurrentUrl();
        clearOwnedHistoryEntry();
        if (shouldTraverseBack) {
            window.history.back();
        }
    }

    function handleOpenChange(nextOpen: boolean) {
        if (!nextOpen) {
            consumeOwnedHistoryEntry();
            onClose();
        }
    }

    beforeNavigate(({ cancel, to, type, willUnload }) => {
        if (willUnload || !browser || type === 'popstate' || !ownsHistoryEntry || !to || historyEntryUrl !== getCurrentUrl()) {
            return;
        }

        pendingNavigation = { options: type === 'link' ? selectedLinkNavigationOptions : undefined, url: to.url };
        cancel();
        clearOwnedHistoryEntry();
        window.history.back();
    });

    onMount(() => {
        function handleDocumentClick(event: MouseEvent): void {
            const link = event.composedPath().find((target): target is HTMLAnchorElement => target instanceof HTMLAnchorElement);
            selectedLinkNavigationOptions = link ? getLinkNavigationOptions(link) : undefined;
        }

        function handlePopState(event: PopStateEvent): void {
            if (pendingNavigation) {
                const navigation = pendingNavigation;
                pendingNavigation = undefined;
                wasOpen = false;
                // Let SvelteKit finish accepting the shallow popstate before
                // beginning the requested route navigation.
                pendingNavigationTimer = window.setTimeout(() => {
                    if (navigation.options) {
                        void goto(navigation.url, navigation.options);
                    } else {
                        void goto(navigation.url);
                    }

                    onClose();
                }, 0);
                return;
            }

            const entry = getHistoryEntry(event.state);
            if (entry) {
                restoreOwnedHistoryEntry(entry);
                return;
            }

            if (!open || !ownsHistoryEntry) {
                return;
            }

            clearOwnedHistoryEntry();
            wasOpen = false;
            onClose();
        }

        document.addEventListener('click', handleDocumentClick, true);
        window.addEventListener('popstate', handlePopState);

        const entry = getHistoryEntry(window.history.state);
        if (entry) {
            restoreOwnedHistoryEntry(entry);
        }

        historyReady = true;
        return () => {
            document.removeEventListener('click', handleDocumentClick, true);
            window.removeEventListener('popstate', handlePopState);
            window.clearTimeout(parentCloseTimer);
            window.clearTimeout(pendingNavigationTimer);
        };
    });

    $effect(() => {
        if (!browser || !historyReady) {
            return;
        }

        if (open && historyValue && !wasOpen) {
            window.clearTimeout(parentCloseTimer);
            const url = getCurrentUrl();
            pushState(url, createHistoryState(historyValue));
            historyEntryUrl = url;
            historyEntryValue = historyValue;
            ownsHistoryEntry = true;
        } else if (open && historyValue && ownsHistoryEntry && historyEntryValue !== historyValue) {
            replaceState(getCurrentUrl(), createHistoryState(historyValue));
            historyEntryValue = historyValue;
        } else if (!open && wasOpen) {
            // Parent-driven closes can be followed immediately by a URL update (for
            // example, applying a filter from the sheet). Wait for that update, then
            // either consume an unchanged same-URL entry or clear the marker while
            // preserving the newer browser URL.
            parentCloseTimer = window.setTimeout(() => {
                const currentEntry = getHistoryEntry(window.history.state);
                const shouldTraverseBack = ownsHistoryEntry && currentEntry?.value === historyEntryValue && historyEntryUrl === getBrowserUrl();

                if (shouldTraverseBack) {
                    clearOwnedHistoryEntry();
                    window.history.back();
                } else {
                    if (ownsHistoryEntry && currentEntry?.value === historyEntryValue) {
                        replaceState('', createHistoryState());
                    }

                    clearOwnedHistoryEntry();
                }
            }, 0);
        }

        wasOpen = open;
    });
</script>

<Sheet.Root onOpenChange={handleOpenChange} {open}>
    <Sheet.Content
        class="bg-background top-15.25! bottom-0! z-40 h-auto! w-full scrollbar-gutter-stable gap-0 overflow-y-auto rounded-l-lg border-l text-base shadow-2xl duration-150 ease-out will-change-transform sm:max-w-full! md:w-5/6!"
        onInteractOutside={preserveDetailSheetForAssistant}
        overlayProps={{ class: 'top-15.25! z-40 bg-black/5 dark:bg-black/40 supports-backdrop-filter:backdrop-blur-[0.5px]' }}
        preventScroll={false}
    >
        <div class="absolute top-3 right-12 z-10 flex items-center gap-1">
            {@render actions?.()}
            <Button aria-label="Open details in new window" href={detailsHref} size="icon-sm" title="Open in new window" variant="ghost">
                <ExternalLink aria-hidden="true" />
            </Button>
        </div>
        <Sheet.Header class="sr-only">
            <Sheet.Title level={3}>{title}</Sheet.Title>
        </Sheet.Header>
        <div class="px-4 pt-4 pb-4">
            {@render children()}
        </div>
    </Sheet.Content>
</Sheet.Root>
