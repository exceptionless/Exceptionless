<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { Muted } from '$comp/typography';
    import { organization } from '$features/organizations/context.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import { SvelteURL } from 'svelte/reactivity';

    const queryParams = createQueryParameters({
        schema: {
            view: 'string'
        }
    });
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return page.params.projectId ?? '';
            }
        }
    });

    $effect(() => {
        const project = projectQuery.data;
        if (!projectQuery.isSuccess || !project || organization.current !== project.organization_id) {
            return;
        }

        const destination = new SvelteURL(page.url);
        destination.pathname = queryParams.view === 'stacks' ? resolve('/(app)/stack') : resolve('/(app)/event');
        destination.searchParams.delete('view');
        destination.searchParams.set('organization', project.organization_id);
        destination.searchParams.set('project', project.id);
        void goto(destination, {
            replaceState: true
        });
    });
</script>

<Muted>Opening project report...</Muted>
