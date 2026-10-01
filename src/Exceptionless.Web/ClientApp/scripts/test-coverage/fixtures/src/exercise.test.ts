import { mount, unmount } from 'svelte';
import { expect, test } from 'vitest';

import Card from './card.svelte';
import { choose, parse } from './exercise';

test('coverage contract executes one arm and throws before the return', async () => {
    expect(choose(true)).toBe('left');
    expect(() => parse('{')).toThrow();
    const target = document.createElement('div');
    const card = mount(Card, { props: { flag: true }, target });
    expect(target.textContent).toBe('left');
    await unmount(card);
});
