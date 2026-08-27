<script module lang="ts">
    import { Column, Heading, Link, Row, Section, Text } from '@better-svelte-email/components';

    export const socialFooterStyles = `
[data-email-social]>tbody>tr>td{padding:0}
[data-email-social] a{line-height:1.3}
[data-email-social] table[role="presentation"] tr:not(:last-child)>td{padding-bottom:16px}
@media only screen and (min-width:597px){[data-social-column]:first-child>h5,[data-social-column]:first-child>table{width:calc(100% - .375px)}[data-social-column]:last-child>h5,[data-social-column]:last-child>p{position:relative;left:-.375px;width:calc(100% + .375px)}}
@media only screen and (max-width:596px){[data-social-column]{display:block!important;width:100%!important;max-width:100%!important;padding-left:16px!important;padding-right:16px!important}[data-social-column]:first-child{padding-bottom:32px!important}}
`;

    // Equal columns need a 0.375px gutter correction to preserve the old
    // Foundation layout's subpixel rounding inside the 580px email container.
</script>

<Section data-email-social class="bg-background p-4">
    <Row style="table-layout:fixed">
        <Column data-social-column width="50%" style="vertical-align:top;padding:0 8px 0 16px">
            <Heading as="h5" class="text-foreground mt-0 mb-[5px] text-[20px] leading-[1.3] font-normal">Connect With Us:</Heading>
            <table width="100%" cellpadding="0" cellspacing="4" role="presentation">
                <tbody>
                    <tr>
                        <td
                            ><a
                                href="https://www.facebook.com/exceptionless/"
                                style="display:block;background:#3B5998;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px"
                                >Facebook</a
                            ></td
                        >
                    </tr>
                    <tr>
                        <td
                            ><a
                                href="https://twitter.com/exceptionless"
                                style="display:block;background:#1daced;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px"
                                >Twitter</a
                            ></td
                        >
                    </tr>
                    <tr>
                        <td
                            ><a
                                href="https://github.com/exceptionless/exceptionless"
                                style="display:block;background:#444444;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px"
                                >GitHub</a
                            ></td
                        >
                    </tr>
                </tbody>
            </table>
        </Column>
        <Column data-social-column width="50%" style="vertical-align:top;padding:0 16px 0 8px">
            <Heading as="h5" class="text-foreground mt-0 mb-[5px] text-[20px] leading-[1.3] font-normal">Contact Info:</Heading>
            <Text class="text-foreground m-0 mb-[10px] text-base leading-[1.3]"
                >Email: <Link href="mailto:support@exceptionless.io" class="text-primary no-underline">support@exceptionless.io</Link></Text
            >
        </Column>
    </Row>
</Section>
