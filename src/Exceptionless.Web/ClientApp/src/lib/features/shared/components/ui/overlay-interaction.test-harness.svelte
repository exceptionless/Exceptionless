<script lang="ts">
    import * as DropdownMenu from './dropdown-menu';
    import * as Popover from './popover';
    import * as Tooltip from './tooltip';

    let dropdownOpen = $state(false);
    let popoverOpen = $state(false);
    let tooltipOpen = $state(false);
</script>

<Tooltip.Provider>
    <Tooltip.Root bind:open={tooltipOpen} suppressWhenOverlayOpen>
        <Tooltip.Trigger>Tag</Tooltip.Trigger>
        <Tooltip.Content>Tag actions</Tooltip.Content>
    </Tooltip.Root>

    <Popover.Root bind:open={popoverOpen}>
        <Popover.Trigger>Filter</Popover.Trigger>
        <Popover.Content>Filter options</Popover.Content>
    </Popover.Root>

    <DropdownMenu.Root bind:open={dropdownOpen}>
        <DropdownMenu.Trigger>Views</DropdownMenu.Trigger>
        <DropdownMenu.Content>
            <DropdownMenu.Group>
                <DropdownMenu.Item>Save As...</DropdownMenu.Item>
            </DropdownMenu.Group>
        </DropdownMenu.Content>
    </DropdownMenu.Root>
</Tooltip.Provider>

<output data-testid="tooltip-state">{tooltipOpen ? 'open' : 'closed'}</output>
<output data-testid="popover-state">{popoverOpen ? 'open' : 'closed'}</output>
<output data-testid="dropdown-state">{dropdownOpen ? 'open' : 'closed'}</output>
