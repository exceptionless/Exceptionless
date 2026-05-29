<script lang="ts">
    import { afterNavigate } from '$app/navigation';
    import { Exceptionless } from '@exceptionless/browser';

    import { normalizeRouteId } from './route';

    interface Props {
        userId?: string;
        userName?: string;
    }

    let { userId, userName }: Props = $props();

    afterNavigate(async ({ to }) => {
        await Exceptionless.createFeatureUsage(normalizeRouteId(to?.route.id ?? null))
        .setProperty('params', to?.params)
        .submit();
    });

    $effect(() => {
        Exceptionless.config.setUserIdentity(userId ?? '', userName ?? '');
    });
</script>
