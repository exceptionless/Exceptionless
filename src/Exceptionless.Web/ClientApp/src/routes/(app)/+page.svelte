<script lang="ts">
    import { goto } from '$app/navigation';

    import { getContinueSetupHref, isProjectAwaitingFirstEvent } from '#features/onboarding/project-status.ts';
    import { getOrganizationQuery, getOrganizationsQuery } from '#features/organizations/api.svelte.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { getOrganizationProjectsQuery } from '#features/projects/api.svelte.ts';
    import { getSavedViewsQuery, isSavedViewDeleted } from '#features/saved-views/api.svelte.ts';
    import { getSavedViewDefaultHref, resolveSavedViewDefaults } from '#features/saved-views/defaults.ts';
    import { getMeQuery } from '#features/users/api.svelte.ts';

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
    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    // First-run users belong in setup, not an empty Stacks view. Impersonated organizations keep the normal landing.
    const setupHref = $derived.by(() => {
        const projects = (projectsQuery.data?.data ?? []).filter((project) => project.organization_id === organization.current);
        if (!projectsQuery.isSuccess || !membershipOrganization || projects.some((project) => !isProjectAwaitingFirstEvent(project))) {
            return undefined;
        }

        return getContinueSetupHref(projects);
    });
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
            projectsQuery.isPending ||
            (organizationIdToLoad && currentOrganizationQuery.isPending)
        ) {
            return;
        }

        if (setupHref) {
            void goto(`${setupHref}${setupHref.endsWith('/configure') ? '?redirect=true' : ''}`, {
                replace: true
            });
            return;
        }

        void goto(getSavedViewDefaultHref(defaults, savedViews), {
            replace: true
        });
    });
</script>
