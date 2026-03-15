<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';

    goto(resolve('/(app)/system/overview'), { replaceState: true });
</script>
