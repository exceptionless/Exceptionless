<script module lang="ts">
    import { Section, Row, Column, Heading, Link, Text } from '@better-svelte-email/components';
</script>

<Section class="bg-bg p-4">
    <Row>
        <Column width="50%" style="vertical-align:top;padding:8px">
            <Heading as="h5" class="text-[20px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >Connect With Us:</Heading
            >
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
        <Column width="50%" style="vertical-align:top;padding:8px">
            <Heading as="h5" class="text-[20px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >Contact Info:</Heading
            >
            <Text class="text-base text-dark leading-[1.3] m-0 mb-[10px]"
                >Email: <Link href="mailto:support@exceptionless.io" class="text-primary no-underline"
                    >support@exceptionless.io</Link
                ></Text
            >
        </Column>
    </Row>
</Section>
