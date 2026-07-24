<script lang="ts">
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import Logo from '$comp/logo.svelte';
    import { Button } from '$comp/ui/button';
    import { accessToken } from '$features/auth/index.svelte';
    import { documentationHref, supportIssuesHref } from '$features/shared/help-links';
    import ArrowRightIcon from '@lucide/svelte/icons/arrow-right';
    import BookOpenIcon from '@lucide/svelte/icons/book-open';
    import FolderKanbanIcon from '@lucide/svelte/icons/folder-kanban';
    import LogInIcon from '@lucide/svelte/icons/log-in';
    import SearchXIcon from '@lucide/svelte/icons/search-x';
    import ShieldAlertIcon from '@lucide/svelte/icons/shield-alert';
    import SparklesIcon from '@lucide/svelte/icons/sparkles';

    const isAuthenticated = $derived(Boolean(accessToken.current));
    const isNotFound = $derived(page.status === 404);
    const statusLabel = $derived(isNotFound ? '404' : String(page.status));
    const title = $derived(getErrorTitle(page.status));
    const message = $derived(getErrorMessage(page.status, page.error?.message));
    const primaryHref = $derived(isAuthenticated ? resolve('/(app)/event') : resolve('/(auth)/login'));
    const primaryLabel = $derived(isAuthenticated ? 'Open Events' : 'Log in');

    function getErrorTitle(status: number): string {
        if (status === 403) {
            return "You don't have access to this page";
        }

        if (status === 404) {
            return "We couldn't find that page";
        }

        if (status === 410) {
            return 'That page is no longer available';
        }

        if (status === 426) {
            return 'This page needs a plan change';
        }

        if (status >= 500) {
            return 'Something went wrong on our side';
        }

        return 'This page hit an error';
    }

    function getErrorMessage(status: number, fallback?: string): string {
        if (status === 403) {
            return 'Your account is signed in, but it does not have permission to open this resource.';
        }

        if (status === 404) {
            return 'The page may have moved, the link may be stale, or the item may no longer exist.';
        }

        if (status === 410) {
            return 'The item may have been removed, archived, or replaced by a newer location.';
        }

        if (status === 426) {
            return 'The resource exists, but your current plan or account state cannot open it yet.';
        }

        if (status >= 500) {
            return 'The request reached Exceptionless, but the service could not finish it. Try again in a moment.';
        }

        return fallback ?? 'Something unexpected happened while opening this page.';
    }
</script>

<svelte:head>
    <title>{isNotFound ? 'Page Not Found' : 'Error'} - Exceptionless</title>
</svelte:head>

<main class="bg-background text-foreground relative flex min-h-screen overflow-hidden" aria-labelledby="error-title">
    <div
        class="absolute inset-0 bg-[linear-gradient(to_right,hsl(var(--border))_1px,transparent_1px),linear-gradient(to_bottom,hsl(var(--border))_1px,transparent_1px)] bg-size-[48px_48px] opacity-20"
    ></div>
    <div class="via-primary/70 absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent to-transparent"></div>

    <section
        class="relative mx-auto grid w-full max-w-6xl items-center gap-10 px-6 py-12 lg:grid-cols-[minmax(0,1fr)_22rem] lg:px-10"
        aria-describedby="error-description"
    >
        <div class="max-w-3xl">
            <Logo class="mx-0 mb-12 max-h-12" />

            <div class="border-primary/30 bg-primary/10 text-primary mb-8 inline-flex items-center gap-2 rounded-full border px-3 py-1 text-sm font-medium">
                {#if isNotFound}
                    <SearchXIcon class="size-4" aria-hidden="true" />
                {:else}
                    <ShieldAlertIcon class="size-4" aria-hidden="true" />
                {/if}
                <span>Status {statusLabel}</span>
            </div>

            <h1 id="error-title" class="max-w-2xl text-4xl leading-tight font-semibold tracking-normal text-balance sm:text-5xl lg:text-6xl">{title}</h1>
            <p id="error-description" class="text-muted-foreground mt-5 max-w-2xl text-base leading-7 sm:text-lg">{message}</p>

            <div class="mt-8 flex flex-col gap-3 sm:flex-row">
                <Button href={primaryHref} size="xl" class="w-full sm:w-auto">
                    {#if isAuthenticated}
                        <SparklesIcon aria-hidden="true" />
                    {:else}
                        <LogInIcon aria-hidden="true" />
                    {/if}
                    {primaryLabel}
                </Button>
                {#if isAuthenticated}
                    <Button href={resolve('/(app)/project/list')} variant="outline" size="xl" class="w-full sm:w-auto">
                        <FolderKanbanIcon aria-hidden="true" />
                        View Projects
                    </Button>
                {:else}
                    <Button href={documentationHref} variant="outline" size="xl" class="w-full sm:w-auto">
                        <BookOpenIcon aria-hidden="true" />
                        Documentation
                    </Button>
                {/if}
            </div>
        </div>

        <aside class="border-border bg-card/80 rounded-xl border p-5 shadow-2xl shadow-black/30 backdrop-blur" aria-label="Recovery options">
            <div class="mb-5 flex items-center justify-between gap-4">
                <div>
                    <p class="text-muted-foreground text-sm font-medium">Recovery</p>
                    <h2 class="text-xl font-semibold">Next best places</h2>
                </div>
                <div class="bg-primary/10 text-primary flex size-10 items-center justify-center rounded-lg">
                    <ArrowRightIcon class="size-5" aria-hidden="true" />
                </div>
            </div>

            <nav aria-label="Error page shortcuts" class="space-y-2">
                <a
                    class="group border-border bg-background/60 hover:bg-muted focus-visible:ring-ring/50 flex items-center justify-between rounded-lg border px-3 py-3 text-sm transition-colors focus-visible:ring-3 focus-visible:outline-none"
                    href={primaryHref}
                >
                    <span>{primaryLabel}</span>
                    <ArrowRightIcon class="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                </a>
                {#if isAuthenticated}
                    <a
                        class="group border-border bg-background/60 hover:bg-muted focus-visible:ring-ring/50 flex items-center justify-between rounded-lg border px-3 py-3 text-sm transition-colors focus-visible:ring-3 focus-visible:outline-none"
                        href={resolve('/(app)/stack')}
                    >
                        <span>Open Stacks</span>
                        <ArrowRightIcon class="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                    </a>
                    <a
                        class="group border-border bg-background/60 hover:bg-muted focus-visible:ring-ring/50 flex items-center justify-between rounded-lg border px-3 py-3 text-sm transition-colors focus-visible:ring-3 focus-visible:outline-none"
                        href={resolve('/(app)/project/list')}
                    >
                        <span>View Projects</span>
                        <ArrowRightIcon class="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                    </a>
                {/if}
                <a
                    class="group border-border bg-background/60 hover:bg-muted focus-visible:ring-ring/50 flex items-center justify-between rounded-lg border px-3 py-3 text-sm transition-colors focus-visible:ring-3 focus-visible:outline-none"
                    href={documentationHref}
                >
                    <span>Documentation</span>
                    <ArrowRightIcon class="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                </a>
                <a
                    class="group border-border bg-background/60 hover:bg-muted focus-visible:ring-ring/50 flex items-center justify-between rounded-lg border px-3 py-3 text-sm transition-colors focus-visible:ring-3 focus-visible:outline-none"
                    href={supportIssuesHref}
                >
                    <span>Support</span>
                    <ArrowRightIcon class="text-muted-foreground size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                </a>
            </nav>
        </aside>
    </section>
</main>
