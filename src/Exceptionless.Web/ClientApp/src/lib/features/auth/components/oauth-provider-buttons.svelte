<script lang="ts">
    import type { Component } from 'svelte';

    import { toast } from 'svelte-sonner';

    import FacebookIcon from '#comp/icons/FacebookIcon.svelte';
    import GitHubIcon from '#comp/icons/GitHubIcon.svelte';
    import GoogleIcon from '#comp/icons/GoogleIcon.svelte';
    import MicrosoftIcon from '#comp/icons/MicrosoftIcon.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import {
        facebookClientId,
        facebookLogin,
        gitHubClientId,
        githubLogin,
        googleClientId,
        googleLogin,
        liveLogin,
        microsoftClientId
    } from '#features/auth/index.svelte.ts';

    import { getOAuthErrorMessage } from '../oauth-errors';

    interface Props {
        disabled?: boolean;
        inviteToken: null | string;
        /** Used in each button's accessible name, for example "Sign up with GitHub". */
        mode: 'login' | 'signup';
        redirectUrl: string;
    }

    interface Provider {
        enabled: boolean;
        icon: Component<{ class?: string }>;
        id: string;
        label: string;
        login: (redirectUrl?: string, inviteToken?: null | string) => Promise<void>;
    }

    let { disabled = false, inviteToken, mode, redirectUrl }: Props = $props();

    const providers: Provider[] = [
        {
            enabled: !!microsoftClientId,
            icon: MicrosoftIcon,
            id: 'microsoft',
            label: 'Microsoft',
            login: liveLogin
        },
        {
            enabled: !!googleClientId,
            icon: GoogleIcon,
            id: 'google',
            label: 'Google',
            login: googleLogin
        },
        {
            enabled: !!facebookClientId,
            icon: FacebookIcon,
            id: 'facebook',
            label: 'Facebook',
            login: facebookLogin
        },
        {
            enabled: !!gitHubClientId,
            icon: GitHubIcon,
            id: 'github',
            label: 'GitHub',
            login: githubLogin
        }
    ].filter((provider) => provider.enabled);

    let pendingProviderId = $state<string>();
    let toastId: number | string | undefined;

    async function signIn(provider: Provider): Promise<void> {
        toast.dismiss(toastId);
        pendingProviderId = provider.id;

        try {
            await provider.login(redirectUrl, inviteToken);
        } catch (error: unknown) {
            const { description, title } = getOAuthErrorMessage(error, provider.label);
            toastId = toast.error(title, {
                description
            });
        } finally {
            pendingProviderId = undefined;
        }
    }
</script>

{#if providers.length > 0}
    <!-- An odd last provider spans both columns so one or three providers leave no gap. -->
    <div class="grid gap-2 sm:grid-cols-2 sm:[&>*:last-child:nth-child(odd)]:col-span-2">
        {#each providers as provider (provider.id)}
            {@const Icon = provider.icon}
            <Button
                aria-label={`${mode === 'signup' ? 'Sign up' : 'Log in'} with ${provider.label}`}
                class="w-full"
                disabled={disabled || !!pendingProviderId}
                onclick={() => signIn(provider)}
                size="lg"
                variant="outline"
            >
                {#if pendingProviderId === provider.id}
                    <Spinner data-icon="inline-start" />
                {:else}
                    <Icon class="size-4" />
                {/if}
                {provider.label}
            </Button>
        {/each}
    </div>
{/if}
