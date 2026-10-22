<script lang="ts">
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import GitHubIcon from '#comp/icons/GitHubIcon.svelte';
    import GoogleIcon from '#comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '#comp/icons/MicrosoftIcon.svelte';
    import { H4, Muted, P } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { Separator } from '#comp/ui/separator/index.ts';
    import * as Table from '#comp/ui/table/index.ts';
    import { unlinkOAuthAccount } from '#features/auth/api.svelte.ts';
    import {
        enableOAuthLogin,
        gitHubClientId,
        githubLogin,
        googleClientId,
        googleLogin,
        microsoftClientId,
        microsoftLogin
    } from '#features/auth/index.svelte.ts';
    import { getMeQuery } from '#features/users/api.svelte.ts';

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
    const activeOAuthAccountCount = $derived(oauthAccounts.filter((account) => !['facebook', 'windowslive'].includes(account.provider.toLowerCase())).length);

    function canUnlinkAccount(provider: string) {
        return hasLocalAccount || ['facebook', 'windowslive'].includes(provider.toLowerCase()) || activeOAuthAccountCount > 1;
    }
</script>

<div class="space-y-6">
    <div>
        <Muted>Manage your connected social accounts for single sign-on</Muted>
    </div>
    <Separator />

    {#if enableOAuthLogin}
        <div>
            <H4>Add an external login</H4>
            <div class="mt-2 flex flex-wrap gap-2">
                {#if microsoftClientId}
                    <Button aria-label="Link Microsoft account" onclick={() => microsoftLogin()} variant="outline">
                        <MicrosoftIcon class="size-4" /> Microsoft
                    </Button>
                {/if}
                {#if googleClientId}
                    <Button aria-label="Link Google account" onclick={() => googleLogin()} variant="outline">
                        <GoogleIcon class="size-4" /> Google
                    </Button>
                {/if}
                {#if gitHubClientId}
                    <Button aria-label="Link GitHub account" onclick={() => githubLogin()} variant="outline">
                        <GitHubIcon class="size-4" /> GitHub
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
                                        disabled={!canUnlinkAccount(account.provider)}
                                        onclick={() => handleUnlinkAccount(account.provider, account.provider_user_id)}
                                        size="icon"
                                        title={canUnlinkAccount(account.provider) ? 'Remove' : 'Cannot remove the only login method'}
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
