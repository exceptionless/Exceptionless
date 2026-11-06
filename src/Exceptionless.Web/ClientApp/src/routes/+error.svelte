<script lang="ts">
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ArrowRightIcon from '@lucide/svelte/icons/arrow-right';
    import BookOpenIcon from '@lucide/svelte/icons/book-open';
    import FolderKanbanIcon from '@lucide/svelte/icons/folder-kanban';
    import LogInIcon from '@lucide/svelte/icons/log-in';
    import SearchXIcon from '@lucide/svelte/icons/search-x';
    import ShieldAlertIcon from '@lucide/svelte/icons/shield-alert';
    import SparklesIcon from '@lucide/svelte/icons/sparkles';

    import Logo from '#comp/logo.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { accessToken } from '#features/auth/index.svelte.ts';
    import { getErrorMessage, getErrorTitle } from '#features/shared/error-page.ts';
    import { documentationHref, supportIssuesHref } from '#features/shared/help-links.ts';

    const isAuthenticated = $derived(Boolean(accessToken.current));
    const isNotFound = $derived(page.status === 404);
    const statusLabel = $derived(isNotFound ? '404' : String(page.status));
    const title = $derived(getErrorTitle(page.status));
    const message = $derived(getErrorMessage(page.status, page.error?.message));
    const primaryHref = $derived(isAuthenticated ? resolve('/(app)/event') : resolve('/(auth)/login'));
    const primaryLabel = $derived(isAuthenticated ? 'Open Events' : 'Log in');
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
