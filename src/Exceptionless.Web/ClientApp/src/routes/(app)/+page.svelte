<script lang="ts">
    import { goto } from '$app/navigation';
    import { getOrganizationQuery, getOrganizationsQuery } from '$features/organizations/api.svelte';
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
    const membershipOrganization = $derived(organizationsQuery.data?.data?.find((organizationItem) => organizationItem.id === organization.current));
    const organizationIdToLoad = $derived(organizationsQuery.isSuccess && !membershipOrganization ? organization.current : undefined);
    const currentOrganizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationIdToLoad;
            }
        }
    });
    const currentOrganization = $derived(membershipOrganization ?? currentOrganizationQuery.data);
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
        if (
            !organization.current ||
            currentUserQuery.isPending ||
            organizationsQuery.isPending ||
            savedViewsQuery.isPending ||
            (organizationIdToLoad && currentOrganizationQuery.isPending)
        ) {
            return;
        }

        void goto(getSavedViewDefaultHref(defaults, savedViews), {
            replaceState: true
        });
    });
</script>
