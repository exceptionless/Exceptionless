<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { page } from '$app/state';
    import { Exceptionless } from '@exceptionless/browser';

    import { endSession, setUserIdentity } from '#features/auth/exceptionless-session.ts';
    import { isShallowNavigationWithinPage } from '#features/navigation/shallow-navigation.ts';

    import { normalizePath, normalizeRouteId } from './route';

    interface Props {
        authenticated: boolean;
        userId?: string;
        userName?: string;
    }

    let { authenticated, userId, userName }: Props = $props();

    afterNavigate(async (navigation) => {
        // Shallow routing (query parameters, detail sheets) and Back or Forward through it stay on the same page.
        if (isShallowNavigationWithinPage(navigation)) {
            return;
        }

        if (page.status === 404) {
            await Exceptionless.submitNotFound(normalizePath(page.url.pathname));
        } else {
            await Exceptionless.createFeatureUsage(normalizeRouteId(navigation.to?.route.id ?? null))
                .setProperty('params', navigation.to?.params)
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
