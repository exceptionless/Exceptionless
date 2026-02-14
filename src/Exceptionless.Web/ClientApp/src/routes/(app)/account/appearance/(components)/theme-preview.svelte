<script lang="ts">
    import { systemPrefersMode } from 'mode-watcher';

    interface Props {
        mode: 'dark' | 'light' | 'system';
    }

    let { mode }: Props = $props();

    const resolvedMode = $derived(mode !== 'system' ? mode : (systemPrefersMode.current ?? 'dark'));
</script>

{#if resolvedMode === 'light'}
    <div class="border-muted hover:border-accent items-center rounded-md border-2 p-1">
        <div class="space-y-2 rounded-sm bg-[hsl(210,20%,98%)] p-2">
            <!-- secondary-->
            <div class="space-y-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-xs">
                <!--card -->
                <div class="h-2 w-20 rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
                <!--muted-foreground -->
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-xs">
                <div class="size-4 rounded-full bg-[hsl(240,3.8%,46.1%)]"></div>
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-xs">
                <div class="size-4 rounded-full bg-[hsl(240,3.8%,46.1%)]"></div>
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
        </div>
    </div>
{:else}
    <div class="border-muted bg-popover hover:bg-accent hover:text-accent-foreground items-center rounded-md border-2 p-1">
        <div class="space-y-2 rounded-sm bg-[hsl(225,21%,7%)] p-2">
            <!-- secondary-->
            <div class="space-y-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-xs">
                <!--card -->
                <div class="h-2 w-20 rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
                <!--muted-foreground -->
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-xs">
                <div class="size-4 rounded-full bg-[hsl(240,5%,64.9%)]"></div>
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-xs">
                <div class="size-4 rounded-full bg-[hsl(240,5%,64.9%)]"></div>
                <div class="h-2 w-25 rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
        </div>
    </div>
{/if}
