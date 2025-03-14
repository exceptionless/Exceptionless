<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { page } from '$app/state';
    import { Button } from '$comp/ui/button';
    import { cubicInOut } from 'svelte/easing';
    import { crossfade } from 'svelte/transition';

    type Props = HTMLAttributes<HTMLButtonElement> & {
        href: string;
    };

    let { class: className, href, title }: Props = $props();
    const isActive = $derived(page.url.pathname === href);

    const [send, receive] = crossfade({
        duration: 250,
        easing: cubicInOut
    });
</script>

<Button class={[!isActive && 'hover:underline', 'relative justify-start hover:bg-transparent', className]} data-sveltekit-noscroll {href} variant="ghost">
    {#if isActive}
        <div class="bg-muted absolute inset-0 rounded-md" in:send={{ key: 'active-sidebar-tab' }} out:receive={{ key: 'active-sidebar-tab' }}></div>
    {/if}
    <div class="relative">
        {title}
    </div>
</Button>
