<script lang="ts">
    import * as Field from '$comp/ui/field';
    import { Switch } from '$comp/ui/switch';
    import { getMeQuery } from '$features/users/api.svelte';
    import { toast } from 'svelte-sonner';

    import { putProductTourAnalytics } from '../api.svelte';

    const meQuery = getMeQuery();
    const preference = putProductTourAnalytics();

    async function update(enabled: boolean): Promise<void> {
        try {
            await preference.mutateAsync(enabled);
            toast.success('Guide usage preference saved.');
        } catch {
            toast.error('Could not save your guide usage preference. Please try again.');
        }
    }
</script>

<section id="guided-tour-privacy" aria-label="Guided-tour privacy">
    <Field.FieldGroup>
        <Field.Field orientation="horizontal" data-disabled={preference.isPending || !meQuery.data}>
            <Field.FieldContent>
                <Field.Label for="product-tour-analytics">Help improve guided tours</Field.Label>
                <Field.FieldDescription id="product-tour-analytics-description">
                    Share guide starts, steps reached, completions, and dismissals. We use this only to improve guided tours. Your progress is saved even when
                    this is off. Turning this off stops future collection, not previously recorded activity.
                </Field.FieldDescription>
            </Field.FieldContent>
            <Switch
                id="product-tour-analytics"
                aria-describedby="product-tour-analytics-description"
                checked={meQuery.data?.product_tour_analytics_enabled ?? false}
                disabled={preference.isPending || !meQuery.data}
                onCheckedChange={update}
            />
        </Field.Field>
    </Field.FieldGroup>
</section>
