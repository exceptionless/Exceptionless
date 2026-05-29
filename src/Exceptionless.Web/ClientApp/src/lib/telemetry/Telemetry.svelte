<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { page } from '$app/state';
    import { Exceptionless } from '@exceptionless/browser';

    import { normalizePath, normalizeRouteId } from './route';

    interface Props {
        userId?: string;
        userName?: string;
    }

    let { userId, userName }: Props = $props();

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
        Exceptionless.config.setUserIdentity(userId ?? '', userName ?? '');
    });
</script>
