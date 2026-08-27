<script module lang="ts">
    import { Head, Html } from '@better-svelte-email/components';
</script>

<Html lang="en">
    <Head>
        <meta charset="utf-8" />
        <title>{'{{Subject}}'}</title>
    </Head>
    <svelte:element this={"body"} class="text-foreground" style="font-family:Helvetica,Arial,sans-serif;line-height:1.5">
        <h1 style="font-size:22px;font-weight:400">{'{{Subject}}'}</h1>
        <dl>
            <dt>Name</dt>
            <dd>{'{{Name}}'}</dd>
            <dt>Email</dt>
            <dd>{'{{EmailAddress}}'}</dd>
            {@html '{{#if Company}}'}
            <dt>Company</dt>
            <dd>{'{{Company}}'}</dd>
            {@html '{{/if}}'}
            <dt>Subject</dt>
            <dd>{'{{RequestSubject}}'}</dd>
        </dl>

        <h2 style="font-size:18px;font-weight:400">Message</h2>
        {@html '{{#each MessageLines}}'}
        <p>{'{{this}}'}</p>
        {@html '{{/each}}'}

        <hr />
        <dl>
            {@html '{{#if ClientIpAddress}}'}
            <dt>IP address</dt>
            <dd>{'{{ClientIpAddress}}'}</dd>
            {@html '{{/if}}'}
            {@html '{{#if UserAgent}}'}
            <dt>User agent</dt>
            <dd>{'{{UserAgent}}'}</dd>
            {@html '{{/if}}'}
            {@html '{{#if Referrer}}'}
            <dt>Referrer</dt>
            <dd>{'{{Referrer}}'}</dd>
            {@html '{{/if}}'}
        </dl>
    </svelte:element>
</Html>
