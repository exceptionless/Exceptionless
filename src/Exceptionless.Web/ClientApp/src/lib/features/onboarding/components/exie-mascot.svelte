<script lang="ts">
    import { cn } from '#lib/utils.ts';

    import type { ExieMood } from '../models';

    interface Props {
        class?: string;
        mood?: ExieMood;
        /** Rendered width and height in pixels. */
        size?: number;
    }

    let { class: className, mood = 'idle', size = 72 }: Props = $props();

    const happy = $derived(mood === 'celebrate' || mood === 'hello');
</script>

<!--
    Exie at mascot size. The geometry is ExieIcon's (24×24 grid): the rounded head, side ears,
    two eyes, and the four-point sparkle antenna. Extra parts only add expression.
-->
<svg
    aria-hidden="true"
    class={cn('exie-mascot text-foreground shrink-0 overflow-visible', `exie-${mood}`, className)}
    data-mood={mood}
    focusable="false"
    height={size}
    viewBox="-4 -2 32 28"
    width={size}
    xmlns="http://www.w3.org/2000/svg"
>
    <g class="exie-body">
        <g class="exie-head" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.6">
            <path d="M12 8V7" />
            <rect class="fill-card" height="12" rx="2" width="16" x="4" y="8" />
            <path class="exie-ear-left" d="M2 14h2" />
            <path class="exie-ear-right" d="M20 14h2" />

            <g class="exie-eyes">
                {#if happy}
                    <path d="M8 14.4q1-1.6 2 0" />
                    <path d="M14 14.4q1-1.6 2 0" />
                {:else}
                    <path d="M9 13v2" />
                    <path d="M15 13v2" />
                {/if}
            </g>

            {#if happy}
                <path class="exie-smile" d="M10.4 16.8q1.6 1.3 3.2 0" />
            {/if}
        </g>

        <circle class="exie-signal fill-primary" cx="12" cy="3.75" r="3.75" />
        <path
            class="exie-sparkle fill-primary"
            d="M12 0C12.32 2.48 13.28 3.43 15.75 3.75C13.28 4.07 12.32 5.03 12 7.5C11.68 5.03 10.72 4.07 8.25 3.75C10.72 3.43 11.68 2.48 12 0Z"
        />
    </g>

    {#if mood === 'celebrate'}
        <g class="exie-burst fill-primary">
            <path class="exie-burst-1" d="M2 3c.16 1.24.64 1.72 1.88 1.88C2.64 5.04 2.16 5.52 2 6.76 1.84 5.52 1.36 5.04.12 4.88 1.36 4.72 1.84 4.24 2 3Z" />
            <path class="exie-burst-2" d="M22 2c.16 1.24.64 1.72 1.88 1.88-1.24.16-1.72.64-1.88 1.88-.16-1.24-.64-1.72-1.88-1.88C21.36 3.72 21.84 3.24 22 2Z" />
            <path class="exie-burst-3" d="M-1 13c.12.93.48 1.29 1.41 1.41-.93.12-1.29.48-1.41 1.41-.12-.93-.48-1.29-1.41-1.41.93-.12 1.29-.48 1.41-1.41Z" />
            <path class="exie-burst-4" d="M25 12c.12.93.48 1.29 1.41 1.41-.93.12-1.29.48-1.41 1.41-.12-.93-.48-1.29-1.41-1.41.93-.12 1.29-.48 1.41-1.41Z" />
        </g>
    {/if}

    {#if mood === 'thinking'}
        <g class="exie-thought fill-muted-foreground">
            <circle class="exie-thought-1" cx="21.5" cy="5.5" r=".7" />
            <circle class="exie-thought-2" cx="23.5" cy="3.5" r=".9" />
            <circle class="exie-thought-3" cx="26" cy="1" r="1.1" />
        </g>
    {/if}
</svg>

<style>
    .exie-mascot :global(*) {
        transform-box: fill-box;
    }

    .exie-body,
    .exie-head {
        transform-origin: 50% 100%;
    }

    .exie-eyes,
    .exie-sparkle,
    .exie-signal,
    .exie-burst path,
    .exie-thought circle {
        transform-origin: center;
    }

    .exie-ear-right {
        transform-origin: 0% 50%;
    }

    .exie-signal {
        opacity: 0;
    }

    /* Static expressions keep each mood readable when motion is reduced. */
    .exie-listening .exie-eyes {
        transform: translateX(-0.8px);
    }

    .exie-thinking .exie-eyes {
        transform: translate(0.9px, -0.7px);
    }

    .exie-listening .exie-signal {
        opacity: 0.18;
    }

    @media (prefers-reduced-motion: no-preference) {
        .exie-idle .exie-body {
            animation: exie-bob 3.2s ease-in-out infinite;
        }

        .exie-idle .exie-eyes,
        .exie-hello .exie-eyes {
            animation: exie-blink 4.8s ease-in-out infinite;
        }

        .exie-hello .exie-head {
            animation: exie-tilt 2.4s ease-in-out 1 both;
        }

        .exie-hello .exie-ear-right {
            animation: exie-wave 0.5s ease-in-out 4 alternate;
        }

        .exie-hello .exie-body {
            animation: exie-bob 3.2s ease-in-out 2.4s infinite;
        }

        .exie-listening .exie-eyes {
            animation: exie-look 3.6s ease-in-out infinite;
        }

        .exie-listening .exie-sparkle {
            animation: exie-pulse 1.8s ease-in-out infinite;
        }

        .exie-listening .exie-signal {
            animation: exie-signal 1.8s ease-out infinite;
        }

        .exie-celebrate .exie-body {
            animation: exie-hop 0.9s cubic-bezier(0.3, 0, 0.3, 1) 3;
        }

        .exie-celebrate .exie-sparkle {
            animation: exie-spin 0.9s ease-in-out 3;
        }

        .exie-celebrate .exie-burst path {
            animation: exie-burst 1.4s ease-out infinite;
        }

        .exie-celebrate .exie-burst-2 {
            animation-delay: 0.2s;
        }

        .exie-celebrate .exie-burst-3 {
            animation-delay: 0.45s;
        }

        .exie-celebrate .exie-burst-4 {
            animation-delay: 0.65s;
        }

        .exie-thinking .exie-body {
            animation: exie-bob 4s ease-in-out infinite;
        }

        .exie-thinking .exie-thought circle {
            animation: exie-thought 1.5s ease-in-out infinite;
        }

        .exie-thinking .exie-thought-2 {
            animation-delay: 0.2s;
        }

        .exie-thinking .exie-thought-3 {
            animation-delay: 0.4s;
        }
    }

    @keyframes exie-bob {
        0%,
        100% {
            transform: translateY(0);
        }

        50% {
            transform: translateY(-0.8px);
        }
    }

    @keyframes exie-blink {
        0%,
        92%,
        100% {
            transform: scaleY(1);
        }

        95% {
            transform: scaleY(0.1);
        }
    }

    @keyframes exie-tilt {
        0%,
        100% {
            transform: rotate(0);
        }

        20% {
            transform: rotate(-7deg);
        }

        45% {
            transform: rotate(5deg);
        }

        70% {
            transform: rotate(-3deg);
        }
    }

    @keyframes exie-wave {
        from {
            transform: rotate(0);
        }

        to {
            transform: rotate(-35deg);
        }
    }

    @keyframes exie-look {
        0%,
        100% {
            transform: translateX(-1px);
        }

        40%,
        50% {
            transform: translateX(1px);
        }
    }

    @keyframes exie-pulse {
        0%,
        100% {
            transform: scale(1);
        }

        50% {
            transform: scale(1.18);
        }
    }

    @keyframes exie-signal {
        0% {
            opacity: 0.35;
            transform: scale(0.6);
        }

        100% {
            opacity: 0;
            transform: scale(2);
        }
    }

    @keyframes exie-hop {
        0%,
        100% {
            transform: translateY(0) scaleY(1);
        }

        15% {
            transform: translateY(0) scaleY(0.94);
        }

        45% {
            transform: translateY(-3px) scaleY(1.03);
        }

        75% {
            transform: translateY(0) scaleY(0.97);
        }
    }

    @keyframes exie-spin {
        0%,
        100% {
            transform: rotate(0) scale(1);
        }

        50% {
            transform: rotate(45deg) scale(1.25);
        }
    }

    @keyframes exie-burst {
        0% {
            opacity: 0;
            transform: scale(0.2);
        }

        30% {
            opacity: 1;
            transform: scale(1.1);
        }

        100% {
            opacity: 0;
            transform: scale(0.6);
        }
    }

    @keyframes exie-thought {
        0%,
        100% {
            opacity: 0.35;
        }

        50% {
            opacity: 1;
        }
    }
</style>
