<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { Muted } from '$comp/typography';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import { toast } from 'svelte-sonner';
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

    let isRedirecting = $state(false);

    $effect(() => {
        if (isRedirecting) {
            return;
        }

        if (projectQuery.isError) {
            isRedirecting = true;
            toast.error(`The project "${page.params.projectId}" could not be found.`);
            void goto(resolve('/(app)/project/list'), {
                replaceState: true
            });
            return;
        }
        const project = projectQuery.data;
        if (!projectQuery.isSuccess || !project) {
            return;
        }

        // Let the app layout select the authorized owner before the destination's filters mount.
        isRedirecting = true;
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
