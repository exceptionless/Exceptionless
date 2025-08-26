<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import Boolean from '$comp/formatters/boolean.svelte';
    import { Button } from '$comp/ui/button';
    import { Label } from '$comp/ui/label';
    import * as Popover from '$comp/ui/popover';
    import * as RadioGroup from '$comp/ui/radio-group';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        changed: (value?: boolean) => void;
        open: boolean;
        remove: () => void;
        title: string;
        value?: boolean;
    }

    let { changed, open = $bindable(), remove, title, value }: Props = $props();

    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state(value);

    let radioValue = $derived(updatedValue === undefined ? 'no-value' : updatedValue === true ? 'yes' : 'no');

    $effect(() => {
        updatedValue = value;
    });

    function handleRadioChange(selectedValue: string) {
        if (selectedValue === 'yes') {
            updatedValue = true;
        } else if (selectedValue === 'no') {
            updatedValue = false;
        } else if (selectedValue === 'no-value') {
            updatedValue = undefined;
        }
    }

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter') {
            event.preventDefault();
            onClose();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            onCancel();
        }
    }

    function onClose() {
        if (updatedValue !== value) {
            changed(updatedValue);
        }

        open = false;
    }

    function onCancel() {
        updatedValue = value;
        open = false;
    }

    function onOpenChange(isOpen: boolean) {
        if (!isOpen) {
            onClose();
        }
    }

    export function onClearFilter() {
        updatedValue = undefined;
    }
</script>

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        <Button class="gap-x-1 px-3" size="lg" variant="outline">
            {title}
            <Separator class="mx-2" orientation="vertical" />
            {#if value !== undefined}
                <FacetedFilter.BadgeValue><Boolean {value} /></FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom">
        <div class="border-b p-4">
            <RadioGroup.Root
                value={radioValue}
                onValueChange={handleRadioChange}
                onkeydown={handleKeyDown}
                aria-describedby={`${title}-help`}
                aria-label={title}
            >
                <div class="flex items-center space-x-2">
                    <RadioGroup.Item value="yes" id="boolean-yes" />
                    <Label for="boolean-yes">Yes</Label>
                </div>
                <div class="flex items-center space-x-2">
                    <RadioGroup.Item value="no" id="boolean-no" />
                    <Label for="boolean-no">No</Label>
                </div>
                <div class="flex items-center space-x-2">
                    <RadioGroup.Item value="no-value" id="boolean-no-value" />
                    <Label for="boolean-no-value">No Value</Label>
                </div>
            </RadioGroup.Root>
        </div>
        <div id="{title}-help" class="sr-only">Press Enter to apply filter, Escape to cancel</div>
        <FacetedFilter.Actions clear={onClearFilter} close={onClose} {remove} showClear={updatedValue !== undefined} />
    </Popover.Content>
</Popover.Root>
