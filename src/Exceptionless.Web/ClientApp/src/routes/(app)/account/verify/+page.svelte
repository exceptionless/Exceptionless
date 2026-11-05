<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useFetchClient } from '@foundatiofx/fetchclient';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { toast } from 'svelte-sonner';

    import { queryKeys as userQueryKeys } from '#features/users/api.svelte.ts';

    const client = useFetchClient();
    const queryClient = useQueryClient();
    const token = page.url.searchParams.get('token');

    async function verifyAccount() {
        if (token) {
            try {
                await client.get(`/users/verify-email-address/${encodeURIComponent(token)}`);
                await queryClient.resetQueries({
                    exact: true,
                    queryKey: userQueryKeys.me()
                });
                toast.success('Your account has been successfully verified.');
            } catch {
                toast.error('An error occurred while verifying your account.');
            }
        }

        await goto(resolve('/(app)/account/manage'));
    }

    verifyAccount();
</script>
