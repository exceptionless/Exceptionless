import { Exceptionless } from "@exceptionless/browser"

const options = new URL(import.meta.url).searchParams
const apiKey = options.get("apiKey")

if (apiKey) {
  await Exceptionless.startup((clientConfig) => {
    clientConfig.apiKey = apiKey
    clientConfig.includePrivateInformation = false
    clientConfig.defaultTags.push("Website")

    const serverUrl = options.get("serverUrl")
    if (serverUrl) {
      clientConfig.serverUrl = serverUrl
    }

    const version = options.get("version")
    if (version) {
      clientConfig.version = version
    }

    clientConfig.addPlugin("site-context", 10, async (context) => {
      context.event.data = context.event.data || {}
      context.event.data["@page"] = location.pathname
      context.event.data["@page.url"] = `${location.origin}${location.pathname}`
      context.event.data["@page.title"] = document.title
    })
  })
}
