<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { page } from '$app/state';
    import { endSession, setUserIdentity } from '$features/auth/exceptionless-session';
    import { Exceptionless } from '@exceptionless/browser';

    import { normalizePath, normalizeRouteId } from './route';

    interface Props {
        authenticated: boolean;
        userId?: string;
        userName?: string;
    }

    let { authenticated, userId, userName }: Props = $props();

    afterNavigate(async ({ to }) => {
        if (page.status === 404) {
            await Exceptionless.submitNotFound(normalizePath(page.url.pathname));
        } else {
            await Exceptionless.createFeatureUsage(normalizeRouteId(to?.route.id ?? null))
                .setProperty('params', to?.params)
                .submit();
        }
    });

    $effect(() => {
        if (!authenticated) {
            void endSession();
        } else if (userId) {
            void setUserIdentity(userId, userName);
        }
    });
</script>
