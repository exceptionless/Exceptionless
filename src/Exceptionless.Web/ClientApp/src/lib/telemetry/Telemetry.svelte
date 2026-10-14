<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { page } from '$app/state';
    import { Exceptionless } from '@exceptionless/browser';

    import { endSession, setUserIdentity } from '#features/auth/exceptionless-session.ts';

    import { normalizePath, normalizeRouteId } from './route';

    interface Props {
        authenticated: boolean;
        userId?: string;
        userName?: string;
    }

    let { authenticated, userId, userName }: Props = $props();

    afterNavigate(async ({ shallow, to, type }) => {
        // Shallow routing (query parameters, detail sheets) stays on the same page.
        if (shallow && type === 'goto') {
            return;
        }

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
