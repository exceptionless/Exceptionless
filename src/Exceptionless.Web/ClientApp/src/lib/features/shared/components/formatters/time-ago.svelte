<script lang="ts">
    import Time from 'svelte-time';

    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();
</script>

<Time format="MMM D, YYYY h:mm:ss A [UTC]Z" live={true} relative={true} timestamp={value}></Time>
