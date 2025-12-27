<script lang="ts">
    import GoogleIcon from '$comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '$comp/icons/MicrosoftIcon.svelte';
    import { H3, H4, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import * as Table from '$comp/ui/table';
    import {
        enableOAuthLogin,
        facebookClientId,
        facebookLogin,
        gitHubClientId,
        githubLogin,
        googleClientId,
        googleLogin,
        liveLogin,
        microsoftClientId,
        unlinkOAuthAccount
    } from '$features/auth/index.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import Facebook from '@lucide/svelte/icons/facebook';
    import GitHub from '@lucide/svelte/icons/github';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    let toastId = $state<number | string>();
    const meQuery = getMeQuery();

    async function handleUnlinkAccount(provider: string, providerUserId: string) {
        toast.dismiss(toastId);
        try {
            const response = await unlinkOAuthAccount(provider, providerUserId);
            if (response.ok) {
                await meQuery.refetch();
                toastId = toast.success(`Successfully unlinked ${provider} account.`);
            } else {
                toastId = toast.error('Error unlinking account. Please try again.');
            }
        } catch {
            toastId = toast.error('Error unlinking account. Please try again.');
        }
    }

    const oauthAccounts = $derived(meQuery.data?.o_auth_accounts ?? []);
    const hasLocalAccount = $derived(meQuery.data?.has_local_account ?? false);
    const canUnlinkAccount = $derived(hasLocalAccount || oauthAccounts.length > 1);
</script>

<div class="space-y-6">
    <div>
        <H3>External Logins</H3>
        <Muted>Manage your connected social accounts for single sign-on.</Muted>
    </div>
    <Separator />

    {#if enableOAuthLogin}
        <div>
            <H4>Add an external login</H4>
            <div class="mt-2 flex flex-wrap gap-2">
                {#if microsoftClientId}
                    <Button aria-label="Link Microsoft account" onclick={() => liveLogin()} variant="outline">
                        <MicrosoftIcon class="size-4" /> Microsoft
                    </Button>
                {/if}
                {#if googleClientId}
                    <Button aria-label="Link Google account" onclick={() => googleLogin()} variant="outline">
                        <GoogleIcon class="size-4" /> Google
                    </Button>
                {/if}
                {#if facebookClientId}
                    <Button aria-label="Link Facebook account" onclick={() => facebookLogin()} variant="outline">
                        <Facebook class="size-4" /> Facebook
                    </Button>
                {/if}
                {#if gitHubClientId}
                    <Button aria-label="Link GitHub account" onclick={() => githubLogin()} variant="outline">
                        <GitHub class="size-4" /> GitHub
                    </Button>
                {/if}
            </div>
        </div>

        <div class="mt-6">
            <H4>Existing external logins</H4>
            {#if oauthAccounts.length > 0}
                <Table.Root class="mt-2">
                    <Table.Header>
                        <Table.Row>
                            <Table.Head>Provider</Table.Head>
                            <Table.Head class="w-24 text-right">Actions</Table.Head>
                        </Table.Row>
                    </Table.Header>
                    <Table.Body>
                        {#each oauthAccounts as account (account.provider_user_id)}
                            <Table.Row>
                                <Table.Cell class="capitalize">{account.provider} ({account.username || account.provider_user_id})</Table.Cell>
                                <Table.Cell class="text-right">
                                    <Button
                                        aria-label="Remove {account.provider} account"
                                        disabled={!canUnlinkAccount}
                                        onclick={() => handleUnlinkAccount(account.provider, account.provider_user_id)}
                                        size="icon"
                                        title={canUnlinkAccount ? 'Remove' : 'Cannot remove the only login method'}
                                        variant="ghost"
                                    >
                                        <X class="size-4" />
                                    </Button>
                                </Table.Cell>
                            </Table.Row>
                        {/each}
                    </Table.Body>
                </Table.Root>
            {:else}
                <P class="text-muted-foreground mt-2">No external logins were found.</P>
            {/if}
        </div>
    {:else}
        <P class="text-muted-foreground">External login providers are not configured.</P>
    {/if}
</div>
