<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { organization } from '$features/organizations/context.svelte';

    $effect(() => {
        const organizationId = organization.current;
        const href = organizationId ? resolve('/(app)/organization/[organizationId]/integrations', { organizationId }) : resolve('/(app)/organization/list');

        goto(href, { replaceState: true });
    });
</script>
