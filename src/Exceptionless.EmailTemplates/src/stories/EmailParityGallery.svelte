<script lang="ts">
    import localLogoUrl from '../../../Exceptionless.Web/ClientApp.angular/img/exceptionless-logo.png?url';
    import { parityScenarios } from './parity-scenarios.js';

    type ViewMode = 'side-by-side' | 'overlay' | 'modern' | 'legacy';

    let selectedId = $state(parityScenarios[0].id);
    let viewMode = $state<ViewMode>('side-by-side');
    let overlayOpacity = $state(50);
    let selected = $derived(parityScenarios.find((scenario) => scenario.id === selectedId) ?? parityScenarios[0]);

    function prepare(html: string): string {
        return html.replaceAll('https://be.exceptionless.io/img/exceptionless-logo.png', localLogoUrl);
    }
</script>

<svelte:head>
    <title>Email Template Parity Gallery</title>
</svelte:head>

<div class="gallery">
    <header>
        <div>
            <p class="eyebrow">Legacy → Svelte 5 migration</p>
            <h1>Email template parity gallery</h1>
            <p class="lede">
                Every production template and conditional branch uses the same sample data on both sides. Run
                <code>npm run validate:parity</code> for exact text, action, and pixel assertions.
            </p>
        </div>

        <div class="status" aria-label="Parity validation coverage">
            <strong>{parityScenarios.length}</strong>
            <span>validated scenarios</span>
        </div>
    </header>

    <section class="controls" aria-label="Preview controls">
        <label>
            Template and variant
            <select bind:value={selectedId}>
                {#each parityScenarios as scenario (scenario.id)}
                    <option value={scenario.id}>{scenario.template} — {scenario.variant}</option>
                {/each}
            </select>
        </label>

        <fieldset>
            <legend>View</legend>
            {#each ['side-by-side', 'overlay', 'legacy', 'modern'] as mode (mode)}
                <button class:active={viewMode === mode} type="button" onclick={() => (viewMode = mode as ViewMode)}>
                    {mode.replace('-', ' ')}
                </button>
            {/each}
        </fieldset>

        {#if viewMode === 'overlay'}
            <label class="opacity">
                Modern opacity: {overlayOpacity}%
                <input type="range" min="0" max="100" bind:value={overlayOpacity} />
            </label>
        {/if}
    </section>

    <div class="scenario-title">
        <div>
            <span>{selected.template}</span>
            <strong>{selected.variant}</strong>
        </div>
        <code>{selected.id}</code>
    </div>

    {#if viewMode === 'side-by-side'}
        <div class="side-by-side">
            <section>
                <h2>Legacy baseline</h2>
                <iframe srcdoc={prepare(selected.legacyHtml)} title="Legacy email preview"></iframe>
            </section>
            <section>
                <h2>Svelte 5 output</h2>
                <iframe srcdoc={prepare(selected.modernHtml)} title="Modern email preview"></iframe>
            </section>
        </div>
    {:else if viewMode === 'overlay'}
        <section class="single">
            <h2>Pixel overlay</h2>
            <div class="overlay" style:height="{selected.height}px">
                <iframe srcdoc={prepare(selected.legacyHtml)} title="Legacy email preview"></iframe>
                <iframe
                    class="modern-overlay"
                    style:opacity={overlayOpacity / 100}
                    srcdoc={prepare(selected.modernHtml)}
                    title="Modern email preview"
                ></iframe>
            </div>
        </section>
    {:else}
        <section class="single">
            <h2>{viewMode === 'legacy' ? 'Legacy baseline' : 'Svelte 5 output'}</h2>
            <iframe
                srcdoc={prepare(viewMode === 'legacy' ? selected.legacyHtml : selected.modernHtml)}
                title="{viewMode} email preview"
            ></iframe>
        </section>
    {/if}
</div>

<style>
    :global(body) {
        margin: 0;
        background: #eef1f5;
        color: #172033;
        font-family:
            Inter,
            ui-sans-serif,
            system-ui,
            -apple-system,
            BlinkMacSystemFont,
            'Segoe UI',
            sans-serif;
    }

    .gallery {
        min-height: 100vh;
        padding: 28px;
    }

    header,
    .controls,
    .scenario-title,
    .side-by-side,
    .single {
        width: min(1480px, 100%);
        margin-inline: auto;
    }

    header {
        display: flex;
        align-items: end;
        justify-content: space-between;
        gap: 24px;
        margin-bottom: 24px;
    }

    h1 {
        margin: 4px 0 8px;
        font-size: clamp(28px, 4vw, 44px);
        letter-spacing: -0.04em;
    }

    .eyebrow {
        margin: 0;
        color: #0f7b6c;
        font-size: 12px;
        font-weight: 800;
        letter-spacing: 0.12em;
        text-transform: uppercase;
    }

    .lede {
        max-width: 760px;
        margin: 0;
        color: #536077;
        line-height: 1.6;
    }

    code {
        border-radius: 5px;
        background: #e1e6ec;
        padding: 2px 6px;
        font-size: 12px;
    }

    .status {
        display: grid;
        min-width: 150px;
        border: 1px solid #c9d4df;
        border-radius: 14px;
        background: white;
        padding: 16px 20px;
        box-shadow: 0 10px 30px rgb(23 32 51 / 8%);
    }

    .status strong {
        color: #0f7b6c;
        font-size: 30px;
    }

    .status span {
        color: #536077;
        font-size: 12px;
    }

    .controls {
        display: flex;
        flex-wrap: wrap;
        align-items: end;
        gap: 18px;
        border: 1px solid #c9d4df;
        border-radius: 14px;
        background: white;
        padding: 16px;
    }

    label,
    legend {
        color: #536077;
        font-size: 12px;
        font-weight: 700;
    }

    label {
        display: grid;
        gap: 7px;
    }

    select,
    button {
        min-height: 40px;
        border: 1px solid #b6c2cf;
        border-radius: 8px;
        background: white;
        color: #172033;
        font: inherit;
    }

    select {
        min-width: min(420px, 75vw);
        padding: 0 12px;
    }

    fieldset {
        display: flex;
        gap: 6px;
        margin: 0;
        border: 0;
        padding: 0;
    }

    legend {
        margin-bottom: 7px;
    }

    button {
        padding: 0 12px;
        text-transform: capitalize;
        cursor: pointer;
    }

    button.active {
        border-color: #0f7b6c;
        background: #0f7b6c;
        color: white;
    }

    .opacity {
        min-width: 220px;
    }

    .scenario-title {
        display: flex;
        align-items: end;
        justify-content: space-between;
        gap: 16px;
        padding: 24px 2px 10px;
    }

    .scenario-title div {
        display: grid;
        gap: 2px;
    }

    .scenario-title span {
        color: #536077;
        font-size: 12px;
    }

    .scenario-title strong {
        font-size: 18px;
    }

    .side-by-side {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 16px;
    }

    section {
        min-width: 0;
    }

    section h2 {
        margin: 0;
        border: 1px solid #c9d4df;
        border-bottom: 0;
        border-radius: 12px 12px 0 0;
        background: #172033;
        padding: 10px 14px;
        color: white;
        font-size: 13px;
        letter-spacing: 0.02em;
    }

    iframe {
        display: block;
        width: 100%;
        height: max(70vh, 700px);
        box-sizing: border-box;
        border: 1px solid #c9d4df;
        background: white;
    }

    .overlay {
        position: relative;
        min-height: 500px;
        background: white;
    }

    .overlay iframe {
        position: absolute;
        inset: 0;
        height: 100%;
    }

    .modern-overlay {
        pointer-events: none;
    }

    @media (max-width: 900px) {
        .gallery {
            padding: 16px;
        }

        header {
            align-items: start;
        }

        .status {
            display: none;
        }

        .side-by-side {
            grid-template-columns: 1fr;
        }

        fieldset {
            flex-wrap: wrap;
        }
    }
</style>
