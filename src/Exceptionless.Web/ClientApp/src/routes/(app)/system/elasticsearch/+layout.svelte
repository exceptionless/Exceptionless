<script lang="ts">
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A, Muted } from '$comp/typography';

    let { children } = $props();

    const tabs = [
        { href: resolve('/(app)/system/elasticsearch/overview'), label: 'Overview' },
        { href: resolve('/(app)/system/elasticsearch/indices'), label: 'Indices' },
        { href: resolve('/(app)/system/elasticsearch/backups'), label: 'Backups' }
    ];

    const currentPath = $derived(page.url.pathname);
</script>

<div class="space-y-6">
    <div>
        <Muted>Cluster health, storage metrics, indices, and backup snapshots</Muted>
    </div>

    <nav class="flex gap-1">
        {#each tabs as tab (tab.href)}
            {@const isActive = currentPath === tab.href || currentPath.startsWith(tab.href + '/')}
            <A
                variant="ghost"
                href={tab.href}
                class="rounded-md px-3 py-1.5 text-sm font-medium transition-colors {isActive
                    ? 'bg-secondary text-secondary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'}"
            >
                {tab.label}
            </A>
        {/each}
    </nav>

    {@render children()}
</div>
