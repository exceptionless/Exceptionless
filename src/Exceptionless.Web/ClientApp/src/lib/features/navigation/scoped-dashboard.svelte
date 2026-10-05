<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import { SvelteURLSearchParams } from 'svelte/reactivity';

    let { organizationId, projectId }: { organizationId?: string; projectId?: string } = $props();
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId ?? '';
            }
        }
    });
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId ?? '';
            }
        }
    });

    $effect(() => {
        const id = projectId ? projectQuery.data?.organization_id : organizationQuery.data?.id;
        if (!id) {
            return;
        }
        organization.current = id;
        const query = new SvelteURLSearchParams(page.url.search);
        const view = query.get('view');
        query.delete('view');
        if (projectId) {
            query.set('project', projectId);
        }
        const destination = view === 'stacks' ? resolve('/(app)/stack') : resolve('/(app)/event');
        void goto(`${destination}?${query}${page.url.hash}`, {
            replaceState: true
        });
    });
</script>

{#if projectQuery.isError || organizationQuery.isError}
    <p class="p-6">This dashboard could not be opened. Check that you still have access and try again.</p>
{:else}
    <p class="p-6">Opening dashboard…</p>
{/if}
