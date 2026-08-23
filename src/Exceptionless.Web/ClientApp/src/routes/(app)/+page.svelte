<script lang="ts">
    import { goto } from '$app/navigation';
    import { organization } from '$features/organizations/context.svelte';
    import { getSavedViewDefaultsQuery, getSavedViewsByViewQuery } from '$features/saved-views/api.svelte';
    import { getSavedViewDefaultHref } from '$features/saved-views/defaults';

    const savedViewDefaultsQuery = getSavedViewDefaultsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const stackSavedViewsQuery = getSavedViewsByViewQuery({
        route: {
            get organizationId() {
                return organization.current;
            },
            view: 'stacks'
        }
    });

    $effect(() => {
        if (!organization.current || savedViewDefaultsQuery.isPending) {
            return;
        }

        const configuredDefault = savedViewDefaultsQuery.data?.user_default ?? savedViewDefaultsQuery.data?.organization_default;
        if (!configuredDefault && stackSavedViewsQuery.isPending) {
            return;
        }

        void goto(getSavedViewDefaultHref(savedViewDefaultsQuery.data, stackSavedViewsQuery.data), {
            replaceState: true
        });
    });
</script>
