<script lang="ts">
    import { goto } from '$app/navigation';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getSavedViewsQuery, isSavedViewDeleted } from '$features/saved-views/api.svelte';
    import { getSavedViewDefaultHref, resolveSavedViewDefaults } from '$features/saved-views/defaults';
    import { getMeQuery } from '$features/users/api.svelte';

    const currentUserQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({});
    const savedViewsQuery = getSavedViewsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const currentOrganization = $derived(organizationsQuery.data?.data?.find((organizationItem) => organizationItem.id === organization.current));
    const savedViews = $derived((savedViewsQuery.data ?? []).filter((savedView) => !isSavedViewDeleted(savedView)));
    const defaults = $derived.by(() => {
        return resolveSavedViewDefaults({
            organizationDefaultSavedViewId: currentOrganization?.default_saved_view_id,
            organizationId: organization.current,
            organizationPreferences: currentUserQuery.data?.organization_preferences,
            savedViews
        });
    });

    $effect(() => {
        if (!organization.current || currentUserQuery.isPending || organizationsQuery.isPending || savedViewsQuery.isPending) {
            return;
        }

        void goto(getSavedViewDefaultHref(defaults, savedViews), {
            replaceState: true
        });
    });
</script>
