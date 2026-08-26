export function findScrollableAncestor(element: HTMLElement): HTMLElement | null {
    let parent = element.parentElement;

    while (parent) {
        const { overflowY } = window.getComputedStyle(parent);
        if (overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay') {
            return parent;
        }

        parent = parent.parentElement;
    }

    return null;
}

export function scrollTableToFirstRow(trigger: HTMLElement): void {
    const tableRoot = trigger.closest<HTMLElement>('[data-slot="data-table"]');
    const toolbar = tableRoot?.querySelector<HTMLElement>('[data-slot="data-table-footer"]');
    const tableBody = tableRoot?.querySelector<HTMLElement>('[data-slot="data-table-body"]');
    const scrollContainer = tableRoot ? findScrollableAncestor(tableRoot) : null;
    if (!tableRoot || !toolbar || !tableBody || !scrollContainer) {
        return;
    }

    const toolbarStyles = window.getComputedStyle(toolbar);
    const tableStyles = window.getComputedStyle(tableRoot);
    const stickyTop = Number.parseFloat(toolbarStyles.top) || 0;
    const rowGap = Number.parseFloat(tableStyles.rowGap) || 0;
    const scrollContainerRect = scrollContainer.getBoundingClientRect();
    const tableBodyRect = tableBody.getBoundingClientRect();
    const tableBodyContentTop = scrollContainer.scrollTop + tableBodyRect.top - scrollContainerRect.top - scrollContainer.clientTop;
    const targetScrollTop = Math.max(0, tableBodyContentTop - toolbar.getBoundingClientRect().height - rowGap - stickyTop);

    scrollContainer.scrollTo({ behavior: 'auto', top: targetScrollTop });
}
