<script lang="ts">
    import Time from 'svelte-time';

    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();
</script>

<Time live={true} relative={true} timestamp={value}></Time>
