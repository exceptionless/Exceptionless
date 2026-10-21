<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { useFetchClient } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';

    import Logo from '#comp/logo.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { logout } from '#features/auth/index.svelte.ts';
    import { clearAuthenticationSession } from '#features/auth/session.svelte.ts';

    interface Props {
        /** Users who already belong to an organization can leave setup. */
        canCancel: boolean;
        emailAddress?: string;
    }

    let { canCancel, emailAddress }: Props = $props();

    const queryClient = useQueryClient();
    const client = useFetchClient();
    let isSigningOut = $state(false);

    async function signOut(): Promise<void> {
        isSigningOut = true;
        try {
            await logout(queryClient, client);
        } catch {
            await queryClient.cancelQueries();
            queryClient.clear();
            clearAuthenticationSession();
        }

        await goto(resolve('/(auth)/login'));
    }
</script>

<header class="border-border flex items-center justify-between gap-3 border-b px-4 py-3">
    <Logo class="mx-0 h-8 max-h-none w-auto object-contain" />
    <div class="flex min-w-0 items-center gap-2 text-sm">
        {#if emailAddress}
            <span class="text-muted-foreground hidden min-w-0 truncate sm:inline">Not {emailAddress}?</span>
        {/if}
        <Button disabled={isSigningOut} onclick={signOut} size="sm" variant="ghost">
            {#if isSigningOut}<Spinner data-icon="inline-start" />{/if}
            Sign out
        </Button>
        {#if canCancel}
            <Button href={resolve('/(app)')} size="sm" variant="outline">Cancel</Button>
        {/if}
    </div>
</header>
