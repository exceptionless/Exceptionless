<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useFetchClient } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { toast } from 'svelte-sonner';

    import type { VerifyEmailAddressResult, ViewCurrentUser } from '#features/users/models.ts';

    import { queryKeys as userQueryKeys } from '#features/users/api.svelte.ts';

    const client = useFetchClient();
    const queryClient = useQueryClient();
    const token = page.url.searchParams.get('token');

    async function verifyAccount() {
        if (token) {
            try {
                const response = await client.getJSON<VerifyEmailAddressResult>(`/users/verify-email-address/${encodeURIComponent(token)}`);
                await queryClient.resetQueries({
                    exact: true,
                    queryKey: userQueryKeys.me()
                });

                // The link verifies whoever it was sent to, which may not be the account signed in here.
                const verifiedEmailAddress = response.data?.email_address;
                const currentUser = queryClient.getQueryData<ViewCurrentUser>(userQueryKeys.me());
                if (verifiedEmailAddress && currentUser && verifiedEmailAddress.toLowerCase() !== currentUser.email_address.toLowerCase()) {
                    const stillNeedsVerifying = currentUser.is_email_address_verified ? '' : ', which still needs verifying';
                    toast.warning(`Verified ${verifiedEmailAddress}. You're signed in as ${currentUser.email_address}${stillNeedsVerifying}.`);
                } else {
                    toast.success('Your account has been successfully verified.');
                }
            } catch {
                toast.error('An error occurred while verifying your account.');
            }
        }

        await goto(resolve('/(app)/account/manage'));
    }

    verifyAccount();
</script>
