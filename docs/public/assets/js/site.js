import { initializeSiteSearch } from "./site-search.js"

const root = document.documentElement

initializeTheme()
initializeHeader()
initializeMobileNav()
initializeDocsNav()
initializeTabs()
initializeHeadingAnchors()
initializeCopyButtons()
initializeCopyMarkdown()
initializePageToc()
initializeLightbox()
initializeBillingToggle()
initializeAnchorNav()
initializeReveal()
initializeGitHubStars()

const searchModal = document.getElementById("site-search-modal")
if (searchModal) {
  initializeSiteSearch(searchModal)
}

function initializeTheme() {
  document.querySelectorAll("[data-theme-toggle]").forEach((toggle) => {
    toggle.addEventListener("click", () => {
      const theme = root.dataset.theme === "dark" ? "light" : "dark"
      root.dataset.theme = theme
      try {
        localStorage.setItem("theme", theme)
      } catch {
        // Theme still applies for this page view when storage is unavailable.
      }
    })
  })

  globalThis.matchMedia?.("(prefers-color-scheme: dark)").addEventListener("change", (event) => {
    let stored = null
    try {
      stored = localStorage.getItem("theme")
    } catch {
      stored = null
    }

    if (stored !== "light" && stored !== "dark") {
      root.dataset.theme = event.matches ? "dark" : "light"
    }
  })
}

function initializeHeader() {
  const header = document.querySelector("[data-site-header]")
  if (!header) {
    return
  }

  const update = () => header.classList.toggle("is-scrolled", globalThis.scrollY > 8)
  update()
  globalThis.addEventListener("scroll", update, { passive: true })
}

function initializeMobileNav() {
  const toggle = document.querySelector("[data-nav-toggle]")
  if (!toggle) {
    return
  }

  const setOpen = (open) => {
    document.body.classList.toggle("nav-open", open)
    toggle.setAttribute("aria-expanded", String(open))
    toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu")
  }

  toggle.addEventListener("click", () => setOpen(!document.body.classList.contains("nav-open")))
  document.querySelectorAll("#mobile-nav a, #mobile-nav button").forEach((link) =>
    link.addEventListener("click", () => setOpen(false))
  )
  globalThis.addEventListener("resize", () => {
    if (globalThis.innerWidth > 1120) {
      setOpen(false)
    }
  })
}

function initializeDocsNav() {
  const toggle = document.querySelector("[data-docs-nav-toggle]")
  const sidebar = document.querySelector(".docs-sidebar")
  if (!toggle || !sidebar) {
    return
  }

  const setOpen = (open) => {
    document.body.classList.toggle("docs-nav-open", open)
    toggle.setAttribute("aria-expanded", String(open))
  }

  toggle.addEventListener("click", (event) => {
    event.stopPropagation()
    setOpen(!document.body.classList.contains("docs-nav-open"))
  })

  document.addEventListener("click", (event) => {
    if (document.body.classList.contains("docs-nav-open") && !sidebar.contains(event.target)) {
      setOpen(false)
    }
  })

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setOpen(false)
    }
  })
}

function initializeTabs() {
  document.querySelectorAll("[data-tabs]").forEach((container) => {
    const tabs = Array.from(container.querySelectorAll('[role="tab"]'))
    const panels = tabs.map((tab) => document.getElementById(tab.getAttribute("aria-controls") ?? ""))

    const select = (index, focus = false) => {
      tabs.forEach((tab, tabIndex) => {
        const selected = tabIndex === index
        tab.setAttribute("aria-selected", String(selected))
        tab.tabIndex = selected ? 0 : -1
        if (panels[tabIndex]) {
          panels[tabIndex].hidden = !selected
        }
      })

      if (focus) {
        tabs[index].focus()
      }
    }

    tabs.forEach((tab, index) => {
      tab.addEventListener("click", () => select(index))
      tab.addEventListener("keydown", (event) => {
        if (event.key === "ArrowRight") {
          event.preventDefault()
          select((index + 1) % tabs.length, true)
        } else if (event.key === "ArrowLeft") {
          event.preventDefault()
          select((index - 1 + tabs.length) % tabs.length, true)
        }
      })
    })
  })
}

function initializeHeadingAnchors() {
  document.querySelectorAll(".prose h2[id], .prose h3[id]").forEach((heading) => {
    const anchor = document.createElement("a")
    anchor.className = "heading-anchor"
    anchor.href = `#${heading.id}`
    anchor.textContent = "#"
    anchor.setAttribute("aria-label", `Link to ${heading.textContent}`)
    heading.append(anchor)
  })
}

function initializeCopyButtons() {
  const blocks = document.querySelectorAll(".prose pre, .code-window [role='tabpanel'] pre, .inline-code-block pre")
  blocks.forEach((pre) => {
    const button = document.createElement("button")
    button.type = "button"
    button.className = "copy-code"
    button.textContent = "Copy"
    button.addEventListener("click", async () => {
      const code = pre.querySelector("code") ?? pre
      try {
        await navigator.clipboard.writeText(code.innerText.trimEnd())
        button.textContent = "Copied"
      } catch {
        button.textContent = "Press Ctrl+C"
      }

      setTimeout(() => {
        button.textContent = "Copy"
      }, 1800)
    })
    pre.append(button)
  })
}

function initializeCopyMarkdown() {
  document.querySelectorAll("[data-copy-markdown]").forEach((button) => {
    const label = button.querySelector("[data-copy-label]")
    const defaultLabel = label.textContent
    button.addEventListener("click", async () => {
      const markdown = fetch(button.dataset.copyMarkdown).then(async (response) => {
        if (!response.ok) {
          throw new Error(`Markdown request failed with ${response.status}`)
        }

        return new Blob([await response.text()], { type: "text/plain" })
      })

      try {
        // Passing the pending fetch keeps Safari's user-activation requirement satisfied.
        if (globalThis.ClipboardItem && navigator.clipboard.write) {
          await navigator.clipboard.write([new ClipboardItem({ "text/plain": markdown })])
        } else {
          await navigator.clipboard.writeText(await (await markdown).text())
        }

        label.textContent = "Copied"
      } catch {
        label.textContent = "Copy failed"
      }

      setTimeout(() => {
        label.textContent = defaultLabel
      }, 1800)
    })
  })
}

function initializePageToc() {
  const toc = document.querySelector("[data-page-toc]")
  const content = document.querySelector("[data-page-content]")
  if (!toc || !content || toc.hidden) {
    return
  }

  const links = new Map()
  toc.querySelectorAll("a[href^='#']").forEach((link) => {
    const heading = document.getElementById(decodeURIComponent(link.getAttribute("href").slice(1)))
    if (heading) {
      links.set(heading, link)
    }
  })

  const headings = Array.from(links.keys())
  if (!headings.length) {
    return
  }

  const setActive = (heading) => {
    links.forEach((link) => link.classList.remove("is-active"))
    links.get(heading)?.classList.add("is-active")
  }

  const update = () => {
    const offset = 120
    let current = headings[0]
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top - offset <= 0) {
        current = heading
      } else {
        break
      }
    }

    setActive(current)
  }

  update()
  globalThis.addEventListener("scroll", update, { passive: true })
}

function initializeLightbox() {
  const dialog = document.querySelector("[data-lightbox-dialog]")
  const image = dialog?.querySelector("[data-lightbox-image]")
  const caption = dialog?.querySelector("[data-lightbox-caption]")
  if (!dialog || !image) {
    return
  }

  document.querySelectorAll("[data-lightbox]").forEach((trigger) => {
    trigger.addEventListener("click", (event) => {
      event.preventDefault()
      const visible = Array.from(trigger.querySelectorAll("img")).find((img) => img.offsetParent !== null)
      if (!visible) {
        return
      }

      image.src = visible.dataset.full || visible.currentSrc || visible.src
      image.alt = visible.alt
      if (caption) {
        caption.textContent = trigger.dataset.caption || visible.alt
      }

      dialog.showModal()
    })
  })

  dialog.querySelector("[data-lightbox-close]")?.addEventListener("click", () => dialog.close())
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) {
      dialog.close()
    }
  })
}

function initializeBillingToggle() {
  const buttons = document.querySelectorAll("[data-billing]")
  if (!buttons.length) {
    return
  }

  const select = (period) => {
    buttons.forEach((button) => button.setAttribute("aria-pressed", String(button.dataset.billing === period)))
    document.querySelectorAll("[data-monthly]").forEach((element) => {
      element.textContent = period === "yearly" ? element.dataset.yearly : element.dataset.monthly
    })
  }

  buttons.forEach((button) => button.addEventListener("click", () => select(button.dataset.billing)))
}

function initializeAnchorNav() {
  const nav = document.querySelector("[data-anchor-nav]")
  if (!nav) {
    return
  }

  const links = Array.from(nav.querySelectorAll("a[href^='#']"))
  const sections = links.map((link) => document.querySelector(link.getAttribute("href"))).filter(Boolean)

  const update = () => {
    let current = null
    for (const section of sections) {
      if (section.getBoundingClientRect().top - 180 <= 0) {
        current = section
      }
    }

    links.forEach((link) => {
      const active = current !== null && link.getAttribute("href") === `#${current.id}`
      if (active && !link.classList.contains("is-active")) {
        const list = link.closest("ul")
        if (list) {
          list.scrollLeft = link.offsetLeft - list.clientWidth / 2 + link.offsetWidth / 2
        }
      }

      link.classList.toggle("is-active", active)
    })
  }

  update()
  globalThis.addEventListener("scroll", update, { passive: true })
}

function initializeReveal() {
  const elements = document.querySelectorAll(".reveal")
  if (!elements.length) {
    return
  }

  if (!("IntersectionObserver" in globalThis)) {
    elements.forEach((element) => element.classList.add("is-visible"))
    return
  }

  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add("is-visible")
        observer.unobserve(entry.target)
      }
    })
  }, { rootMargin: "0px 0px -10% 0px" })

  elements.forEach((element) => observer.observe(element))
}

async function initializeGitHubStars() {
  const targets = document.querySelectorAll("[data-github-stars]")
  if (!targets.length) {
    return
  }

  const cacheKey = "github-stars"
  let stars = null
  try {
    const cached = JSON.parse(sessionStorage.getItem(cacheKey) ?? "null")
    if (cached && Date.now() - cached.time < 6 * 60 * 60 * 1000) {
      stars = cached.stars
    }
  } catch {
    stars = null
  }

  if (typeof stars !== "number") {
    try {
      const response = await fetch("https://api.github.com/repos/exceptionless/Exceptionless", {
        headers: { Accept: "application/vnd.github+json" },
      })
      if (!response.ok) {
        return
      }

      stars = (await response.json()).stargazers_count
      if (typeof stars !== "number") {
        return
      }

      sessionStorage.setItem(cacheKey, JSON.stringify({ stars, time: Date.now() }))
    } catch {
      // Keep the build-time star count when GitHub is unavailable or rate limited.
      return
    }
  }

  const label = formatStars(stars)
  targets.forEach((target) => {
    target.textContent = label
  })
}

/** Matches GitHub's compact star count, for example 2458 becomes 2.5k. */
function formatStars(stars) {
  if (stars < 1000) {
    return String(stars)
  }

  return `${(Math.round(stars / 100) / 10).toFixed(1).replace(/\.0$/, "")}k`
}
