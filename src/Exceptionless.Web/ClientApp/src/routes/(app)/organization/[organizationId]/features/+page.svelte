<script lang="ts">
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import { H3, Muted } from '$comp/typography';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { getOrganizationQuery, removeOrganizationFeature, setOrganizationFeature } from '$features/organizations/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import { toast } from 'svelte-sonner';

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

    const KNOWN_FEATURES: { description: string; id: string; name: string }[] = [
        {
            description: 'Allows users to save and reuse filter combinations across dashboard pages.',
            id: 'feature-saved-views',
            name: 'Saved Views'
        }
    ];

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
</script>

{#if organizationQuery.isError}
    <ErrorMessage message="Unable to load organization data." />
{:else if !isGlobalAdmin}
    <ErrorMessage message="You do not have permission to manage features." />
{:else}
    <div class="space-y-6">
        <div>
            <H3>Features</H3>
            <Muted>Enable or disable features for this organization.</Muted>
        </div>
        <Separator />

        <div class="space-y-3">
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
        </div>
    </div>
{/if}
