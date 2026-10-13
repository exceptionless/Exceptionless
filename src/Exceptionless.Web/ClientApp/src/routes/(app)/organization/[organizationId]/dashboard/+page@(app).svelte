<script lang="ts">
    import { page } from '$app/state';
    import ScopedDashboard from '$features/navigation/scoped-dashboard.svelte';
</script>

<ScopedDashboard organizationId={page.params.organizationId} />
