<script lang="ts">
    import { systemPrefersMode } from 'mode-watcher';

    interface Props {
        mode: 'dark' | 'light' | 'system';
    }

    let { mode }: Props = $props();

    let resolvedMode = $state(mode !== 'system' ? mode : ($systemPrefersMode ?? 'dark'));
</script>

{#if resolvedMode === 'light'}
    <div class="items-center rounded-md border-2 border-muted p-1 hover:border-accent">
        <div class="space-y-2 rounded-sm bg-[hsl(210,20%,98%)] p-2">
            <!-- secondary-->
            <div class="space-y-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-sm">
                <!--card -->
                <div class="h-2 w-[80px] rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
                <!--muted-foreground -->
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-sm">
                <div class="size-4 rounded-full bg-[hsl(240,3.8%,46.1%)]"></div>
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(0,0%,100%)] p-2 shadow-sm">
                <div class="size-4 rounded-full bg-[hsl(240,3.8%,46.1%)]"></div>
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,3.8%,46.1%)]"></div>
            </div>
        </div>
    </div>
{:else}
    <div class="items-center rounded-md border-2 border-muted bg-popover p-1 hover:bg-accent hover:text-accent-foreground">
        <div class="space-y-2 rounded-sm bg-[hsl(225,21%,7%)] p-2">
            <!-- secondary-->
            <div class="space-y-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-sm">
                <!--card -->
                <div class="h-2 w-[80px] rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
                <!--muted-foreground -->
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-sm">
                <div class="size-4 rounded-full bg-[hsl(240,5%,64.9%)]"></div>
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
            <div class="flex items-center space-x-2 rounded-md bg-[hsl(222,17%,11%)] p-2 shadow-sm">
                <div class="size-4 rounded-full bg-[hsl(240,5%,64.9%)]"></div>
                <div class="h-2 w-[100px] rounded-lg bg-[hsl(240,5%,64.9%)]"></div>
            </div>
        </div>
    </div>
{/if}
