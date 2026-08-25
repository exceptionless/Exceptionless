namespace Exceptionless.EmailTemplates.Components;

internal static class EmailStyles
{
    public const string Background = "#f7f7f7";
    public const string Border = "#cbcbcb";
    public const string Dark = "#2c2c2c";
    public const string Primary = "#5e9a00";
    public const string PrimaryAction = "#6ebc1a";
    public const string White = "#fefefe";

    public const string Heading1 = "color:#2c2c2c;font-family:Helvetica,Arial,sans-serif;font-size:34px;font-weight:400;line-height:1.3;margin:0 0 5px;text-align:left";
    public const string Heading4 = "color:#2c2c2c;font-family:Helvetica,Arial,sans-serif;font-size:24px;font-weight:400;line-height:1.3;margin:0 0 5px;text-align:left";
    public const string Heading5 = "color:#939393;font-family:Helvetica,Arial,sans-serif;font-size:20px;font-weight:400;line-height:1.3;margin:0 0 5px;text-align:left";
    public const string Lead = "color:#2c2c2c;font-family:Helvetica,Arial,sans-serif;font-size:20px;font-weight:400;line-height:1.6;margin:0 0 10px;text-align:left";
    public const string Paragraph = "color:#2c2c2c;font-family:Helvetica,Arial,sans-serif;font-size:16px;font-weight:400;line-height:1.3;margin:0 0 10px;text-align:left";
    public const string Link = "color:#5e9a00;text-decoration:none";
    public const string ActionLink = "color:#6ebc1a;text-decoration:none";

    public const string ClientCss = """
        :root{color-scheme:light only;supported-color-schemes:light}
        html,body{margin:0!important;padding:0!important;width:100%!important;min-width:100%!important;background:#f7f7f7!important;color:#2c2c2c!important;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%}
        table{border-collapse:collapse;border-spacing:0}
        img{border:0;line-height:100%;outline:none;text-decoration:none}
        @media only screen and (max-width:596px){.email-container{width:95%!important}.email-social-column{display:block!important;width:100%!important;padding:0 16px 24px!important}.email-metric{display:inline-block!important;box-sizing:border-box!important;padding:4px!important}.email-metric-3{width:33.333%!important}.email-metric-4{width:50%!important}}
        """;
}
