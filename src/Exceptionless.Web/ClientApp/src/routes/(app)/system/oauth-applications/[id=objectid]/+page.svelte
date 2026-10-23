<script lang="ts">
    import { page } from '$app/state';

    import ErrorMessage from '#comp/error-message.svelte';
    import { Muted } from '#comp/typography/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { getOAuthApplicationQuery } from '#features/admin/api.svelte.ts';
    import OAuthApplicationForm from '#features/admin/components/oauth-applications/oauth-application-form.svelte';

    const applicationId = $derived(page.params.id);
    const applicationQuery = getOAuthApplicationQuery(() => applicationId);
</script>

<div class="max-w-3xl space-y-6">
    <Muted>Edit the registered OAuth client and its allowed access.</Muted>

    {#if applicationQuery.isPending}
        <div class="text-muted-foreground flex items-center gap-2 py-8 text-sm">
            <Spinner />
            Loading OAuth application...
        </div>
    {:else if applicationQuery.isError}
        <ErrorMessage message="Failed to load the OAuth application." />
    {:else if applicationQuery.data}
        <OAuthApplicationForm application={applicationQuery.data} />
    {/if}
</div>
