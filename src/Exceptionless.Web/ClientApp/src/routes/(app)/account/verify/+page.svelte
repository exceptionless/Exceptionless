<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import { toast } from 'svelte-sonner';

    const client = useFetchClient();
    const token = page.url.searchParams.get('token');

    async function verifyAccount() {
        if (token) {
            try {
                await client.post('/users/verify-email-address', undefined, {
                    params: { token }
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
