<script lang="ts">
    import { resolve } from '$app/paths';
    import { A, Muted } from '$comp/typography';
</script>

<Muted class="text-xs">
    Optional guide usage helps us improve guided tours.
    <A href={`${resolve('/(app)/account/manage')}#guided-tour-privacy`}>Usage settings</A>
</Muted>
