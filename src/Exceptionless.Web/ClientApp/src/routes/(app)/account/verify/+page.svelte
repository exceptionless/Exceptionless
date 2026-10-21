<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    import { Muted } from '#comp/typography/index.ts';
    import { verifyEmailAddress } from '#features/users/api.svelte.ts';

    const queryClient = useQueryClient();

    async function verifyAccount() {
        const token = page.url.searchParams.get('token')?.trim();
        if (!token) {
            toast.error('This verification link is incomplete. Open the link from your email again, or resend the verification email from your account.');
        } else {
            try {
                const result = await verifyEmailAddress(queryClient, token);
                if (result === 'verified') {
                    toast.success('Your email address is verified.');
                } else if (result === 'expired') {
                    toast.error('This verification link has expired. Resend the verification email from your account to get a new link.');
                } else {
                    toast.error(
                        'This verification link is invalid or has already been used. Resend the verification email from your account to get a new link.'
                    );
                }
            } catch {
                toast.error('We could not verify your email address. Try again in a few minutes.');
            }
        }

        await goto(resolve('/(app)/account/manage'));
    }

    onMount(() => {
        void verifyAccount();
    });
</script>

<Muted role="status">Verifying your email address...</Muted>
