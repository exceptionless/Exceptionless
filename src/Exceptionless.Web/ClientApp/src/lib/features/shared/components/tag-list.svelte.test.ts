import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import TagList from './tag-list.svelte';

const resizeObservers: ResizeObserverMock[] = [];

class ResizeObserverMock {
    private observedElements = new Set<Element>();
    disconnect = vi.fn(() => {
        this.observedElements.clear();
    });

    observe = vi.fn((element: Element) => {
        this.observedElements.add(element);
    });

    takeRecords = vi.fn(() => []);
    unobserve = vi.fn((element: Element) => {
        this.observedElements.delete(element);
    });
    private callback: ResizeObserverCallback;
    constructor(callback: ResizeObserverCallback) {
        this.callback = callback;
        resizeObservers.push(this);
    }

    trigger(element: Element) {
        if (this.observedElements.has(element)) {
            this.callback([{ target: element } as ResizeObserverEntry], this as unknown as ResizeObserver);
        }
    }
}

describe('TagList', () => {
    beforeAll(() => {
        vi.stubGlobal('ResizeObserver', ResizeObserverMock);
    });

    beforeEach(() => {
        resizeObservers.length = 0;
    });

    afterAll(() => {
        vi.unstubAllGlobals();
    });

    it('renders neutral tag badges without filter icons and summarizes overflow', () => {
        const { container } = render(TagList, {
            maxVisible: 2,
            tags: ['api', 'production', 'critical', 'customer']
        });

        expect(screen.getByText('api')).toBeTruthy();
        expect(screen.getByText('production')).toBeTruthy();
        expect(screen.getByText('+2')).toBeTruthy();
        expect(screen.queryByText('critical')).toBeNull();
        expect(screen.getByLabelText('Tags: api, production, critical, customer').getAttribute('title')).toBeNull();
        expect(container.querySelector('svg')).toBeNull();

        for (const badge of container.querySelectorAll('[data-slot="badge"]')) {
            expect(badge.classList).toContain('border-border');
            expect(badge.classList).toContain('dark:border-muted-foreground/50');
            expect(badge.classList).toContain('bg-muted');
            expect(badge.classList).toContain('text-muted-foreground');
            expect(badge.classList).toContain('rounded-md');
        }
    });

    it('filters when a tag is clicked', async () => {
        const onTagClick = vi.fn();
        render(TagList, { onTagClick, tags: ['api'] });

        const tagButton = screen.getByRole('button', { name: 'api' });
        await fireEvent.click(tagButton);

        expect(onTagClick).toHaveBeenCalledOnce();
        expect(onTagClick).toHaveBeenCalledWith('api');

        await fireEvent.pointerEnter(tagButton);
        await fireEvent.pointerMove(tagButton);

        const tooltip = document.querySelector('[data-slot="tooltip-content"]');
        const shortcut = tooltip?.querySelector('[data-slot="kbd"]');
        expect(shortcut).not.toBeNull();
        expect(shortcut?.textContent?.trim()).toBe('Alt / Option');
        expect(shortcut?.classList.contains('in-data-[slot=tooltip-content]:bg-muted')).toBe(true);
        expect(shortcut?.classList.contains('in-data-[slot=tooltip-content]:text-foreground')).toBe(true);
        expect(shortcut?.classList.contains('border-border')).toBe(true);
        expect(tooltip?.textContent).not.toContain('api');
    });

    it('exposes a full tag value only when its badge is truncated', async () => {
        const tag = 'a-very-long-tag-value-that-does-not-fit';
        render(TagList, { tags: [tag] });

        const badge = screen.getByText(tag);
        expect(badge.getAttribute('title')).toBeNull();

        Object.defineProperties(badge, {
            clientWidth: { configurable: true, value: 112 },
            scrollWidth: { configurable: true, value: 240 }
        });
        resizeObservers.forEach((observer) => observer.trigger(badge));
        await tick();

        expect(badge.getAttribute('title')).toBe(tag);
    });

    it('includes a truncated tag value in the action tooltip', async () => {
        const tag = 'a-very-long-clickable-tag-value-that-does-not-fit';
        render(TagList, { onTagClick: vi.fn(), tags: [tag] });

        const badge = screen.getByText(tag);
        Object.defineProperties(badge, {
            clientWidth: { configurable: true, value: 112 },
            scrollWidth: { configurable: true, value: 280 }
        });
        resizeObservers.forEach((observer) => observer.trigger(badge));
        await tick();

        const tagButton = screen.getByRole('button', { name: tag });
        await fireEvent.pointerEnter(tagButton);
        await fireEvent.pointerMove(tagButton);

        expect(document.querySelector('[data-slot="tooltip-content"]')?.textContent).toContain(tag);
    });

    it('shows an empty value when there are no tags', () => {
        render(TagList, { tags: [] });

        expect(screen.getByText('—')).toBeTruthy();
    });
});
