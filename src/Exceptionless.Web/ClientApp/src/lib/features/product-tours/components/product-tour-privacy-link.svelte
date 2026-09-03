<script lang="ts">
    import { resolve } from '$app/paths';
    import { A } from '$comp/typography';
</script>

<p class="text-muted-foreground text-xs">
    Optional guide usage helps us improve guided tours.
    <A href={`${resolve('/(app)/account/manage')}#guided-tour-privacy`}>Usage settings</A>
</p>
