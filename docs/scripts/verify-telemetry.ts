const apiKey = (Deno.env.get("EXCEPTIONLESS_SITE_API_KEY") ?? "").trim()
const html = await Deno.readTextFile("_site/index.html")
const scriptPattern = /<script type="module" src="\/assets\/js\/exceptionless-client\.js\?[^"]+"><\/script>/
const scriptMatch = html.match(scriptPattern)

if (!apiKey && scriptMatch) {
  throw new Error("Unconfigured builds must not emit the Exceptionless browser bootstrap")
}

if (apiKey) {
  if (!scriptMatch) {
    throw new Error("Configured builds must emit the Exceptionless browser bootstrap")
  }

  const siteScriptIndex = html.indexOf('<script type="module" src="/assets/js/site.js"></script>')
  if (scriptMatch.index === undefined || scriptMatch.index >= siteScriptIndex) {
    throw new Error("The Exceptionless browser bootstrap must load before site.js")
  }

  await Deno.stat("_site/assets/js/exceptionless-client.js")
}
