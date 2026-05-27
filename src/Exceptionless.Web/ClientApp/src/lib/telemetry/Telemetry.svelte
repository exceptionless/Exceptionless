<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { Exceptionless } from '@exceptionless/browser';

    import { normalizeRouteId } from './index';

    interface Props {
        userId?: string;
        userName?: string;
    }

    let { userId, userName }: Props = $props();

    afterNavigate(({ to }) => {
        void Exceptionless.submitFeatureUsage(normalizeRouteId(to?.route.id ?? null));
    });

    $effect(() => {
        Exceptionless.config.setUserIdentity(userId ?? '', userName ?? '');
    });
</script>
