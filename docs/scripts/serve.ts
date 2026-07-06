const root = new URL("../_site/", import.meta.url)
const port = getPort(Deno.args)

Deno.serve({ hostname: "localhost", port }, async (request) => {
  const requestUrl = new URL(request.url)
  const fileUrl = resolveFileUrl(requestUrl.pathname)

  try {
    const file = await Deno.open(fileUrl, { read: true })
    const contentType = contentTypes[fileUrl.pathname.split(".").pop()?.toLowerCase() ?? ""] ??
      "application/octet-stream"
    return new Response(file.readable, {
      headers: {
        "content-type": contentType,
      },
    })
  } catch (error) {
    if (error instanceof Deno.errors.NotFound) {
      const notFound = new URL("404.html", root)
      return new Response(await Deno.readTextFile(notFound), {
        status: 404,
        headers: {
          "content-type": "text/html; charset=utf-8",
        },
      })
    }

    throw error
  }
})

function getPort(args: string[]): number {
  const portIndex = args.indexOf("--port")
  if (portIndex >= 0) {
    const value = args[portIndex + 1]
    if (value) {
      const parsed = Number(value)
      if (Number.isInteger(parsed) && parsed > 0 && parsed <= 65535) {
        return parsed
      }
    }
  }

  return 3000
}

function resolveFileUrl(pathname: string): URL {
  const decoded = decodeURIComponent(pathname)
  const normalized = decoded.endsWith("/") ? `${decoded}index.html` : decoded
  const withHtmlFallback = normalized.includes(".") ? normalized : `${normalized}/index.html`
  const candidate = new URL(`.${withHtmlFallback}`, root)

  if (!candidate.pathname.startsWith(root.pathname)) {
    return new URL("404.html", root)
  }

  return candidate
}

const contentTypes: Record<string, string> = {
  avif: "image/avif",
  css: "text/css; charset=utf-8",
  gif: "image/gif",
  html: "text/html; charset=utf-8",
  ico: "image/x-icon",
  jpg: "image/jpeg",
  jpeg: "image/jpeg",
  js: "text/javascript; charset=utf-8",
  json: "application/json; charset=utf-8",
  mp4: "video/mp4",
  pdf: "application/pdf",
  png: "image/png",
  svg: "image/svg+xml",
  txt: "text/plain; charset=utf-8",
  webm: "video/webm",
  webp: "image/webp",
  xml: "application/xml; charset=utf-8",
}
