<script lang="ts">
    import { onMount } from 'svelte';

    import { createConfettiPieces } from '../confetti';

    interface Props {
        /** Number of pieces. Kept small so it stays light on low-end devices. */
        count?: number;
    }

    let { count = 90 }: Props = $props();

    const pieces = $derived(createConfettiPieces(count));
    let visible = $state(true);

    onMount(() => {
        // Respect reduced motion: no confetti at all, the celebration copy still says it all.
        if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
            visible = false;
            return;
        }

        const timeout = setTimeout(() => (visible = false), 4200);
        return () => clearTimeout(timeout);
    });
</script>

{#if visible}
    <div aria-hidden="true" class="confetti pointer-events-none fixed inset-0 z-[60] overflow-hidden" data-confetti>
        {#each pieces as piece, index (index)}
            <span
                class="confetti-piece"
                style:--confetti-color={piece.color}
                style:--confetti-delay="{piece.delay}ms"
                style:--confetti-drift="{piece.drift}vw"
                style:--confetti-duration="{piece.duration}ms"
                style:--confetti-rotation="{piece.rotation}deg"
                style:height="{piece.height}px"
                style:left="{piece.left}%"
                style:width="{piece.width}px"
                style:border-radius={piece.round ? '9999px' : '1px'}
            ></span>
        {/each}
    </div>
{/if}

<style>
    .confetti-piece {
        animation: confetti-fall var(--confetti-duration) cubic-bezier(0.2, 0.6, 0.4, 1) var(--confetti-delay) both;
        background: var(--confetti-color);
        position: absolute;
        top: -2vh;
    }

    @media (prefers-reduced-motion: reduce) {
        .confetti {
            display: none;
        }
    }

    @keyframes confetti-fall {
        0% {
            opacity: 1;
            transform: translate3d(0, 0, 0) rotate(0);
        }

        80% {
            opacity: 1;
        }

        100% {
            opacity: 0;
            transform: translate3d(var(--confetti-drift), 105vh, 0) rotate(var(--confetti-rotation));
        }
    }
</style>
