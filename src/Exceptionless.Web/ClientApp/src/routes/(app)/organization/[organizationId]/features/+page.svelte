<script lang="ts">
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { getOrganizationQuery, removeOrganizationFeature, setOrganizationFeature } from '$features/organizations/api.svelte';
    import { postPredefinedSavedViews } from '$features/saved-views/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();

    const organizationId = $derived(page.params.organizationId || '');

    const meQuery = getMeQuery();
    const isGlobalAdmin = $derived(!!meQuery.data?.roles?.includes('global'));

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const organization = $derived(organizationQuery.data);

    const KNOWN_FEATURES: { description: string; id: string; name: string }[] = [];

    function hasFeature(featureId: string) {
        return organization?.features?.includes(featureId) ?? false;
    }

    const setFeature = setOrganizationFeature({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const removeFeature = removeOrganizationFeature({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const predefinedSavedViews = postPredefinedSavedViews({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    async function handleToggleFeature(featureId: string, enabled: boolean) {
        if (!isGlobalAdmin) {
            return;
        }

        const featureName = KNOWN_FEATURES.find((f) => f.id === featureId)?.name ?? featureId;
        const success = enabled ? await setFeature.mutateAsync(featureId) : await removeFeature.mutateAsync(featureId);

        if (success) {
            toast.success(`"${featureName}" feature ${enabled ? 'enabled' : 'disabled'}.`);
        } else {
            toast.error('Failed to update feature. Please try again.');
        }
    }

    async function updatePredefinedSavedViews() {
        toast.dismiss(toastId);
        try {
            const savedViews = await predefinedSavedViews.mutateAsync();
            toastId = toast.success(`Updated ${savedViews.length} predefined saved views.`);
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while updating predefined saved views: ${message}`);
        }
    }
</script>

{#if organizationQuery.isError}
    <ErrorMessage message="Unable to load organization data." />
{:else if !isGlobalAdmin}
    <ErrorMessage message="You do not have permission to manage features." />
{:else}
    <div class="space-y-6">
        <Muted>Manage organization features</Muted>

        <section class="space-y-3" aria-labelledby="saved-views-heading">
            <div class="space-y-1">
                <h2 id="saved-views-heading" class="text-sm font-medium">Saved views</h2>
                <Muted class="text-xs">Update predefined saved views without removing custom saved views.</Muted>
            </div>

            <div class="bg-card rounded-lg border">
                <div class="p-4">
                    <Button
                        class="w-full sm:w-auto"
                        variant="secondary"
                        onclick={updatePredefinedSavedViews}
                        disabled={predefinedSavedViews.isPending || !organizationId}
                    >
                        {#if predefinedSavedViews.isPending}
                            <Spinner />
                            <span>Updating...</span>
                        {:else}
                            <RefreshCw class="mr-2 size-4" aria-hidden="true" />
                            <span>Update Predefined Saved Views</span>
                        {/if}
                    </Button>
                </div>
            </div>
        </section>

        <section class="space-y-3" aria-labelledby="feature-flags-heading">
            <div class="space-y-1">
                <h2 id="feature-flags-heading" class="text-sm font-medium">Feature flags</h2>
                <Muted class="text-xs">Manage feature flags</Muted>
            </div>

            {#if organizationQuery.isLoading}
                {#each Array.from({ length: KNOWN_FEATURES.length }, (_, index) => index) as i (`skeleton-${i}`)}
                    <div class="rounded-lg border p-4">
                        <div class="flex items-center justify-between">
                            <div class="space-y-1">
                                <Skeleton class="h-5 w-32 rounded" />
                                <Skeleton class="h-4 w-64 rounded" />
                            </div>
                            <Skeleton class="h-[1.15rem] w-8 rounded-full" />
                        </div>
                    </div>
                {/each}
            {:else if KNOWN_FEATURES.length === 0}
                <div class="rounded-lg border border-dashed p-6 text-center">
                    <Muted class="text-xs">Feature flags will be available here when they are ready.</Muted>
                </div>
            {:else}
                {#each KNOWN_FEATURES as feature (feature.id)}
                    <div class="rounded-lg border p-4">
                        <div class="flex items-center justify-between">
                            <div>
                                <div class="text-sm font-medium">{feature.name}</div>
                                <Muted class="text-xs">{feature.description}</Muted>
                            </div>
                            <Switch
                                id={feature.id}
                                checked={hasFeature(feature.id)}
                                disabled={setFeature.isPending || removeFeature.isPending}
                                onCheckedChange={(checked) => handleToggleFeature(feature.id, checked)}
                            />
                        </div>
                    </div>
                {/each}
            {/if}
        </section>
    </div>
{/if}
