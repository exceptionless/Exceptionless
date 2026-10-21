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
        /** Where to go when keeping the current session; the invitation is not accepted. */
        continueHref: string;
    }

    let { continueHref }: Props = $props();

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

<!--
    Invitations are accepted only while logging in or signing up with the token, and an existing
    member is added to organizations directly, so an invitation link here is for another account.
-->
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
        <Card.Description>
            This invitation has to be accepted from the invited account. Sign out, then log in or create an account with the email address it was sent to.
        </Card.Description>
    </Card.Header>
    <Card.Footer class="flex flex-col items-stretch gap-2 sm:flex-row sm:flex-wrap sm:items-center">
        <Button disabled={isSigningOut} onclick={signOut}>
            {#if isSigningOut}
                <Spinner data-icon="inline-start" /> Signing out…
            {:else}
                Sign out and accept invitation
            {/if}
        </Button>
        <Button class="max-w-full" href={continueHref} variant="outline">
            <span class="truncate">Continue as {meQuery.data?.email_address ?? 'this account'} without accepting</span>
        </Button>
    </Card.Footer>
</Card.Root>
