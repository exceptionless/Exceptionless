<script lang="ts">
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import Loading from '$comp/Loading.svelte';
    import Switch from '$comp/primitives/Switch.svelte';
    import { H3, H4, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { User } from '$lib/models/api';
    import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';

    const data = $state(new User());
    data.email_notifications_enabled = true;

    const client = useFetchClient();
    let problem = $state(new ProblemDetails());

    async function onSave() {
        if (client.loading) {
            return;
        }

        // let response = await save(data);
        // if (response.ok) {
        //     // TODO
        // } else {
        // 	problem = response.problem;
        // }
    }
</script>

<div class="space-y-6">
    <div>
        <H3>Notifications</H3>
        <Muted>Configure how you receive notifications.</Muted>
    </div>
    <Separator />

    <form class="space-y-2" onsubmit={onSave}>
        <ErrorMessage message={problem.errors.general}></ErrorMessage>

        <H4 class="mb-4">Email Notifications</H4>
        <div class="flex flex-row items-center justify-between rounded-lg border p-4">
            <div class="space-y-0.5">
                <H4>Communication emails</H4>
                <Muted>Receive emails about your account activity.</Muted>
            </div>
            <Switch bind:checked={data.email_notifications_enabled} id="email_notifications_enabled"></Switch>
        </div>

        <div class="pt-2">
            <Button type="submit">
                {#if client.loading}
                    <Loading class="mr-2" variant="secondary"></Loading> Saving...
                {:else}
                    Save
                {/if}
            </Button>
        </div>
    </form>
</div>
