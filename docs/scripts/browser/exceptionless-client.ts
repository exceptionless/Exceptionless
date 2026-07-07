import { Exceptionless } from "@exceptionless/browser"

const options = new URL(import.meta.url).searchParams
const apiKey = options.get("apiKey")

if (apiKey) {
  await Exceptionless.startup((clientConfig) => {
    clientConfig.apiKey = apiKey
    clientConfig.defaultTags.push("Website", "Docs", "Lume")

    const serverUrl = options.get("serverUrl")
    if (serverUrl) {
      clientConfig.serverUrl = serverUrl
    }

    const version = options.get("version")
    if (version) {
      clientConfig.version = version
    }

    const environment = options.get("environment")
    if (environment && environment.toLowerCase() !== "production") {
      clientConfig.useDebugLogger()
      clientConfig.settings["@@log:*"] = "debug"
    }

    clientConfig.useSessions()
    clientConfig.addPlugin("site-context", 10, async (context) => {
      context.event.data = context.event.data || {}
      context.event.data["@page"] = location.pathname
      context.event.data["@page.url"] = location.href
      context.event.data["@page.title"] = document.title
    })
  })
}
