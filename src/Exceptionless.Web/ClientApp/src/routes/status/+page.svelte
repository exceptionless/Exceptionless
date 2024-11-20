<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/stores';
    import Loading from '$comp/Loading.svelte';
    import { H2, P } from '$comp/typography';
    import { getHealthQuery } from '$features/status/api.svelte';
    import logoDark from '$lib/assets/logo-dark.svg';
    import logo from '$lib/assets/logo.svg';

    let redirect = $page.url.searchParams.get('redirect');

    const healthResponse = getHealthQuery();
    $effect(() => {
        if (healthResponse.isSuccess && redirect) {
            goto(redirect, { replaceState: true });
        }
    });
</script>

<div class="flex h-screen">
    <div class="m-auto w-full rounded-md p-6 shadow-md lg:max-w-lg">
        <img alt="Exceptionless Logo" class="mx-auto h-[100px] dark:hidden" src={logo} />
        <img alt="Exceptionless Logo" class="mx-auto hidden h-[100px] dark:block" src={logoDark} />

        <H2 class="mb-2 mt-4 text-center leading-9">Service Status</H2>

        <P class="text-center text-sm">
            {#if healthResponse.isLoading}
                <Loading /> Checking service status...
            {:else if healthResponse.isSuccess}
                Service is healthy.
            {:else}
                We're sorry but the website is currently undergoing maintenance.
                {#if redirect}
                    You'll be automatically redirected when the maintenance is completed.
                {/if}
            {/if}
        </P>
        {#if healthResponse.isLoading || healthResponse.isSuccess}
            <P class="text-center text-sm">If you are currently experiencing an issue please contact support.</P>
        {:else}
            <P class="text-center text-sm">Please contact support for more information.</P>
        {/if}
    </div>
</div>
