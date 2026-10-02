using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace Exceptionless.Web.Security;

internal sealed partial class FrontendScriptNonces
{
    private readonly HashSet<string> _trustedScripts = new(StringComparer.Ordinal);

    public FrontendScriptNonces(IWebHostEnvironment environment) : this(environment.WebRootFileProvider)
    {
    }

    internal FrontendScriptNonces(IFileProvider files)
    {
        // Read only the application's published entry pages, never response or request HTML.
        foreach (string path in new[] { "index.html", "next/index.html" })
        {
            IFileInfo file = files.GetFileInfo(path);
            if (!file.Exists)
                continue;

            using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            foreach (Match script in ScriptElementRegex().Matches(reader.ReadToEnd()))
                _trustedScripts.Add(GetIdentity(script));
        }
    }

    public string AddNonce(string html, string nonce)
    {
        return ScriptElementRegex().Replace(html, script =>
        {
            if (!_trustedScripts.Contains(GetIdentity(script)))
                return script.Value;

            string attributes = RemoveNonce(script.Groups["attributes"].Value);
            return $"<script nonce=\"{nonce}\"{attributes}>{script.Groups["content"].Value}{script.Groups["closingTag"].Value}";
        });
    }

    private static string GetIdentity(Match script)
    {
        string identity = $"<script{RemoveNonce(script.Groups["attributes"].Value)}>{script.Groups["content"].Value}{script.Groups["closingTag"].Value}";
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static string RemoveNonce(string attributes)
    {
        return NonceAttributeRegex().Replace(attributes, attribute => attribute.Groups["quoted"].Success ? attribute.Value : String.Empty);
    }

    [GeneratedRegex("<script\\b(?<attributes>(?:\"[^\"]*\"|'[^']*'|[^'\">])*)>(?<content>.*?)(?<closingTag></script\\s*>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex ScriptElementRegex();

    [GeneratedRegex("(?<quoted>\"[^\"]*\"|'[^']*')|\\snonce(?=[\\s=>/]|$)(?:\\s*=\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s>]+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonceAttributeRegex();
}
