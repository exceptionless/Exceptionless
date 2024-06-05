<script lang="ts">
    import IconFacebook from '~icons/mdi/facebook';
    import IconGitHub from '~icons/mdi/github';
    import IconGoogle from '~icons/mdi/google';
    import IconMicrosoft from '~icons/mdi/microsoft';

    import {
        liveLogin,
        facebookLogin,
        googleLogin,
        githubLogin,
        googleClientId,
        enableOAuthLogin,
        facebookClientId,
        gitHubClientId,
        microsoftClientId
    } from '$api/auth.svelte';

    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { Separator } from '$comp/ui/separator';
    import { Button } from '$comp/ui/button';
    import Loading from '$comp/Loading.svelte';

    import { User } from '$lib/models/api';
    import { useFetchClient, ProblemDetails } from '@exceptionless/fetchclient';
    import PasswordInput from '$comp/form/PasswordInput.svelte';
    import { H3, Muted } from '$comp/typography';

    const data = $state(new User());
    let problem = $state(new ProblemDetails());

    const { loading } = useFetchClient();
    async function onSave() {
        if (loading) {
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
        <H3>Change password</H3>
        <Muted>Update your password to keep your account secure.</Muted>
    </div>
    <Separator />

    <form on:submit|preventDefault={onSave} class="space-y-2">
        <ErrorMessage message={problem.errors.general}></ErrorMessage>

        <PasswordInput
            name="current_password"
            label="Old password"
            bind:value={data.password}
            autocomplete="current-password"
            minlength={6}
            maxlength={100}
            required
            {problem}
            placeholder=""
        ></PasswordInput>

        <PasswordInput
            name="new_password"
            label="New password"
            bind:value={data.password}
            autocomplete="new-password"
            minlength={6}
            maxlength={100}
            required
            {problem}
            placeholder=""
        ></PasswordInput>

        <PasswordInput
            name="confirm_new_password"
            label="Confirm new password"
            bind:value={data.password}
            autocomplete="new-password"
            minlength={6}
            maxlength={100}
            required
            {problem}
            placeholder=""
        ></PasswordInput>

        <Muted>Make sure it's at least 6 characters including a number and a lowercase letter.</Muted>

        <div class="pt-2">
            <Button type="submit">
                {#if loading}
                    <Loading class="mr-2" variant="secondary"></Loading> Updating password...
                {:else}
                    Update password
                {/if}
            </Button>
        </div>
    </form>

    {#if !enableOAuthLogin}
        <div>
            <H3>Connected accounts</H3>
            <Muted>These are the social accounts you connected to your Exceptionless account to log in. You can disable access here.</Muted>
        </div>
        <Separator />

        <ul class="divide-y divide-border">
            {#if !microsoftClientId}
                <li class="pb-4">
                    <div class="flex items-center space-x-4">
                        <IconMicrosoft />
                        <div class="min-w-0 flex-1 font-semibold">Microsoft account</div>
                        <div class="inline-flex items-center">
                            {#if true}
                                <Button aria-label="Disconnect Microsoft account" on:click={() => liveLogin('/next/account/security')} variant="outline">
                                    Disconnect
                                </Button>
                            {:else}
                                <Button aria-label="Connect Microsoft account" on:click={() => liveLogin('/next/account/security')}>Connect</Button>
                            {/if}
                        </div>
                    </div>
                </li>
            {/if}
            {#if !googleClientId}
                <li class="py-4">
                    <div class="flex items-center space-x-4">
                        <IconGoogle />
                        <div class="min-w-0 flex-1 font-semibold">Google account</div>
                        <div class="inline-flex items-center">
                            {#if false}
                                <Button aria-label="Disconnect Google account" on:click={() => googleLogin('/next/account/security')} variant="outline">
                                    Disconnect
                                </Button>
                            {:else}
                                <Button aria-label="Connect Google account" on:click={() => googleLogin('/next/account/security')}>Connect</Button>
                            {/if}
                        </div>
                    </div>
                </li>
            {/if}
            {#if !facebookClientId}
                <li class="py-4">
                    <div class="flex items-center space-x-4">
                        <IconFacebook />
                        <div class="min-w-0 flex-1 font-semibold">Facebook account</div>
                        <div class="inline-flex items-center">
                            {#if false}
                                <Button aria-label="Disconnect Facebook account" on:click={() => facebookLogin('/next/account/security')} variant="outline">
                                    Disconnect
                                </Button>
                            {:else}
                                <Button aria-label="Connect Facebook account" on:click={() => facebookLogin('/next/account/security')}>Connect</Button>
                            {/if}
                        </div>
                    </div>
                </li>
            {/if}
            {#if !gitHubClientId}
                <li class="py-4">
                    <div class="flex items-center space-x-4">
                        <IconGitHub />
                        <div class="min-w-0 flex-1 font-semibold">GitHub account</div>
                        <div class="inline-flex items-center">
                            {#if true}
                                <Button aria-label="Disconnect GitHub account" on:click={() => githubLogin('/next/account/security')} variant="outline">
                                    Disconnect
                                </Button>
                            {:else}
                                <Button aria-label="Connect GitHub account" on:click={() => githubLogin('/next/account/security')}>Connect</Button>
                            {/if}
                        </div>
                    </div>
                </li>
            {/if}
        </ul>
        <div>
            <button
                class="bg-primary-700 hover:bg-primary-800 focus:ring-primary-300 dark:bg-primary-600 dark:hover:bg-primary-700 dark:focus:ring-primary-800 rounded-lg px-5 py-2.5 text-center text-sm font-medium text-white focus:ring-4"
                >Save all</button
            >
        </div>
    {/if}
</div>
