<script lang="ts">
    let { children } = $props();
</script>

<div class="h-dvh w-full overflow-y-auto">
    <div class="flex min-h-full w-full items-start justify-center px-4 py-6 sm:py-8">
        {@render children()}
    </div>
</div>
