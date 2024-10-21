// https://github.com/unplugin/unplugin-icons/pull/381

declare module 'virtual:icons/*' {
    import type { SvelteHTMLElements } from 'svelte/elements';

    import { Component } from 'svelte';

    export default Component<SvelteHTMLElements['svg']>;
}

declare module '~icons/*' {
    import type { SvelteHTMLElements } from 'svelte/elements';

    import { Component } from 'svelte';

    export default Component<SvelteHTMLElements['svg']>;
}
