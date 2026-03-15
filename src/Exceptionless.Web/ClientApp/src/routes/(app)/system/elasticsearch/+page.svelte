<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';

    goto(resolve('/(app)/system/elasticsearch/overview'), { replaceState: true });
</script>
