<script lang="ts">
    import { page } from '$app/state';
    import ScopedDashboard from '$features/navigation/scoped-dashboard.svelte';
</script>

<ScopedDashboard projectId={page.params.projectId} />
