<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import Loading from '$comp/loading.svelte';
    import Logo from '$comp/logo.svelte';
    import { P } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import { getHealthQuery } from '$features/status/api.svelte';

    let redirect = page.url.searchParams.get('redirect');

    const healthQuery = getHealthQuery();
    $effect(() => {
        if (healthQuery.isSuccess && redirect) {
            goto(redirect, { replaceState: true });
        }
    });
</script>

<div class="flex h-screen w-full items-center justify-center">
    <Card.Root class="mx-auto max-w-sm">
        <Card.Header>
            <Logo />
            <Card.Title class="text-center text-2xl" level={2}>Service Status</Card.Title>
        </Card.Header>
        <Card.Content>
            <P class="text-center text-sm">
                {#if healthQuery.isLoading}
                    <Loading /> Checking service status...
                {:else if healthQuery.isSuccess}
                    Service is healthy.
                {:else}
                    We're sorry but the website is currently undergoing maintenance.
                    {#if redirect}
                        You'll be automatically redirected when the maintenance is completed.
                    {/if}
                {/if}
            </P>
            {#if healthQuery.isLoading || healthQuery.isSuccess}
                <P class="text-center text-sm">If you are currently experiencing an issue please contact support.</P>
            {:else}
                <P class="text-center text-sm">Please contact support for more information.</P>
            {/if}
        </Card.Content>
    </Card.Root>
</div>
