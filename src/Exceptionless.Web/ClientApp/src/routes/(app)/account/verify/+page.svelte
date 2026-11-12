<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useFetchClient } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { toast } from 'svelte-sonner';

    import type { VerifyEmailAddressResult } from '#features/users/models.ts';

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

                toast.success(response.data?.email_address ? `Verified ${response.data.email_address}.` : 'Email address verified.');
            } catch {
                toast.error('An error occurred while verifying your account.');
            }
        }

        await goto(resolve('/(app)/account/manage'));
    }

    verifyAccount();
</script>
