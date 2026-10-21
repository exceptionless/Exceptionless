<script lang="ts">
    import { useFetchClient } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { logout } from '#features/auth/index.svelte.ts';
    import { getMeQuery } from '#features/users/api.svelte.ts';

    import { clearAuthenticationSession } from '../session.svelte';

    interface Props {
        /** Where Continue goes. */
        continueHref: string;
        /** Explains what signing out allows, for example accepting an invitation. */
        description: string;
    }

    let { continueHref, description }: Props = $props();

    const queryClient = useQueryClient();
    const client = useFetchClient();
    const meQuery = getMeQuery();
    let isSigningOut = $state(false);

    async function signOut(): Promise<void> {
        isSigningOut = true;
        try {
            await logout(queryClient, client);
        } catch {
            // The server session may already be gone; never leave the form unreachable.
            await queryClient.cancelQueries();
            queryClient.clear();
            clearAuthenticationSession();
        } finally {
            isSigningOut = false;
        }
    }
</script>

<Card.Root class="w-full" data-signed-in-notice>
    <Card.Header>
        <Card.Title>
            {#if meQuery.data}
                You're signed in as {meQuery.data.email_address}
            {:else if meQuery.isError}
                You're already signed in
            {:else}
                <Skeleton class="h-5 w-56" />
            {/if}
        </Card.Title>
        <Card.Description>{description}</Card.Description>
    </Card.Header>
    <Card.Footer class="flex flex-wrap gap-2">
        <Button href={continueHref}>Continue</Button>
        <Button disabled={isSigningOut} onclick={signOut} variant="outline">
            {#if isSigningOut}
                <Spinner data-icon="inline-start" /> Signing out…
            {:else}
                Sign out
            {/if}
        </Button>
    </Card.Footer>
</Card.Root>
