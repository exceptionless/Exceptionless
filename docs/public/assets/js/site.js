const root = document.documentElement
const tabStorageKey = "docs-tab"

initializeTheme()
initializeHeader()
initializeMobileNav()
initializeDocsNav()
initializeTabs()
initializeHeadingAnchors()
initializeCopyButtons()
initializeScrollHints()
initializeCopyMarkdown()
initializePageToc()
initializeLightbox()
initializeBillingToggle()
initializeFaq()
initializeAnchorNav()
initializeReveal()
initializeGitHubStars()

// Load search on its own, with the same ?v= cache-busting query as this file, so the rest of the page doesn't wait for it.
const searchModal = document.getElementById("site-search-modal")
if (searchModal) {
  const searchModuleUrl = new URL(`./site-search.js${new URL(import.meta.url).search}`, import.meta.url).href
  import(searchModuleUrl).then(({ initializeSiteSearch }) => initializeSiteSearch(searchModal))
}

function initializeTheme() {
  const toggles = document.querySelectorAll("[data-theme-toggle]")
  // The theme-color tags follow the OS setting until the page knows the chosen theme; then both use it.
  const themeColors = document.querySelectorAll("meta[name='theme-color']")
  const syncPressed = () => {
    const dark = root.dataset.theme === "dark"
    toggles.forEach((toggle) => toggle.setAttribute("aria-pressed", String(dark)))
    themeColors.forEach((meta) => meta.setAttribute("content", dark ? "#04070b" : "#ffffff"))
    // Eager screenshot pairs load only the active theme's image; load the other one when the theme changes.
    document.querySelectorAll(`img.only-${dark ? "dark" : "light"}[data-src]`).forEach((image) => {
      image.srcset = image.dataset.srcset ?? ""
      image.src = image.dataset.src ?? ""
      image.removeAttribute("data-src")
      image.removeAttribute("data-srcset")
    })
  }
  syncPressed()

  toggles.forEach((toggle) => {
    toggle.addEventListener("click", () => {
      const theme = root.dataset.theme === "dark" ? "light" : "dark"
      root.dataset.theme = theme
      syncPressed()
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
      syncPressed()
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

  // While the menu covers the page, keep keyboard focus out of the content behind it.
  const covered = document.querySelectorAll("#page-content, .site-footer")
  const setOpen = (open) => {
    document.body.classList.toggle("nav-open", open)
    toggle.setAttribute("aria-expanded", String(open))
    toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu")
    covered.forEach((element) => {
      element.inert = open
    })
  }

  toggle.addEventListener("click", () => setOpen(!document.body.classList.contains("nav-open")))
  document.querySelectorAll("#mobile-nav a, #mobile-nav button").forEach((link) =>
    link.addEventListener("click", () => setOpen(false))
  )
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && document.body.classList.contains("nav-open")) {
      setOpen(false)
      toggle.focus()
    }
  })
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

  // Below 900px the sidebar is an off-screen drawer. Keep its links out of the Tab order while it's closed.
  const drawer = globalThis.matchMedia?.("(max-width: 900px)")
  const isOpen = () => document.body.classList.contains("docs-nav-open")
  const syncInert = () => {
    sidebar.inert = Boolean(drawer?.matches) && !isOpen()
  }

  const setOpen = (open, { restoreFocus = false } = {}) => {
    const wasOpen = isOpen()
    document.body.classList.toggle("docs-nav-open", open)
    toggle.setAttribute("aria-expanded", String(open))
    syncInert()
    if (open && !wasOpen) {
      const target = sidebar.querySelector(".toc-active a, [aria-current='page']") ?? sidebar.querySelector("a")
      target?.focus({ preventScroll: true })
    } else if (!open && wasOpen && restoreFocus) {
      toggle.focus()
    }
  }

  syncInert()
  drawer?.addEventListener("change", syncInert)

  toggle.addEventListener("click", (event) => {
    event.stopPropagation()
    setOpen(!isOpen())
  })

  document.addEventListener("click", (event) => {
    if (isOpen() && !sidebar.contains(event.target)) {
      setOpen(false, { restoreFocus: sidebar.contains(document.activeElement) })
    }
  })

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isOpen()) {
      setOpen(false, { restoreFocus: true })
    }
  })
}

function initializeTabs() {
  const syncedSets = []

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

    const choose = (index, focus = false) => {
      select(index, focus)
      const label = tabs[index].dataset.tabLabel
      if (label && container.hasAttribute("data-tab-sync")) {
        syncTabs(syncedSets, label, container)
        try {
          localStorage.setItem(tabStorageKey, label)
        } catch {
          // The choice still applies to this page view when storage is unavailable.
        }
      }
    }

    tabs.forEach((tab, index) => {
      tab.addEventListener("click", () => choose(index))
      tab.addEventListener("keydown", (event) => {
        if (event.key === "ArrowRight") {
          event.preventDefault()
          choose((index + 1) % tabs.length, true)
        } else if (event.key === "ArrowLeft") {
          event.preventDefault()
          choose((index - 1 + tabs.length) % tabs.length, true)
        } else if (event.key === "Home" || event.key === "End") {
          event.preventDefault()
          choose(event.key === "Home" ? 0 : tabs.length - 1, true)
        }
      })
    })

    if (container.hasAttribute("data-tab-sync")) {
      syncedSets.push({ container, tabs, select })
    }
  })

  let stored = null
  try {
    stored = localStorage.getItem(tabStorageKey)
  } catch {
    stored = null
  }

  if (stored) {
    syncTabs(syncedSets, stored)
  }
}

/** Selects the tab with `label` in every synced tab set that has one, except the set the reader used. */
function syncTabs(sets, label, source) {
  for (const set of sets) {
    if (set.container === source) {
      continue
    }

    const index = set.tabs.findIndex((tab) => tab.dataset.tabLabel === label)
    if (index !== -1) {
      set.select(index)
    }
  }
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

/** Copies text and shows the result on the button label for a moment. Falls back to a hint when copying is blocked. */
/** Announces a short status, such as "Copied", to screen readers; a button's own text change isn't announced. */
function announce(message) {
  let region = document.getElementById("site-status")
  if (!region) {
    region = document.createElement("div")
    region.id = "site-status"
    region.className = "sr-only"
    region.setAttribute("role", "status")
    document.body.append(region)
  }

  region.textContent = ""
  setTimeout(() => {
    region.textContent = message
  }, 50)
}

async function copyWithFeedback(text, label) {
  label.dataset.defaultLabel ??= label.textContent
  try {
    await navigator.clipboard.writeText(text)
    label.textContent = "Copied"
    announce("Copied to clipboard")
  } catch {
    label.textContent = "Press Ctrl+C"
    announce("Couldn't copy. Select the text and press Ctrl+C.")
  }

  setTimeout(() => {
    label.textContent = label.dataset.defaultLabel
  }, 1800)
}

function initializeCopyButtons() {
  const blocks = document.querySelectorAll(".prose pre, .code-window [role='tabpanel'] pre, .inline-code-block pre")
  blocks.forEach((pre) => {
    const button = document.createElement("button")
    button.type = "button"
    button.className = "copy-code"
    button.textContent = "Copy"
    button.addEventListener("click", () => {
      const code = pre.querySelector("code") ?? pre
      void copyWithFeedback(code.innerText.trimEnd(), button)
    })

    // The pre scrolls sideways, so a button inside it would scroll away with the code. Put it in a
    // non-scrolling parent instead: the tab panel or inline block that holds this pre, or a wrapper.
    const parent = pre.parentElement
    const host = parent?.matches(".inline-code-block, .code-window [role='tabpanel']") &&
        parent.querySelectorAll("pre").length === 1
      ? parent
      : wrapCodeBlock(pre)
    host.append(button)
  })

  document.querySelectorAll("[data-copy-target]").forEach((button) => {
    const label = button.querySelector("[data-copy-label]") ?? button
    button.addEventListener("click", () => {
      const target = document.getElementById(button.dataset.copyTarget)
      if (target) {
        void copyWithFeedback(target.innerText.trim(), label)
      }
    })
  })
}

function wrapCodeBlock(pre) {
  const wrapper = document.createElement("div")
  wrapper.className = "code-block"
  pre.replaceWith(wrapper)
  wrapper.append(pre)
  return wrapper
}

/** Fades the edges of wide tables while there is more to scroll to, so a clipped column reads as scrollable. */
function initializeScrollHints() {
  document.querySelectorAll(".prose table").forEach((table) => {
    if (table.closest(".table-scroll")) {
      return
    }

    const hint = document.createElement("div")
    hint.className = "scroll-hint prose-table"
    const scroller = document.createElement("div")
    scroller.className = "table-scroll"
    table.replaceWith(hint)
    scroller.append(table)
    hint.append(scroller)
  })

  document.querySelectorAll(".table-scroll, .table-wrap").forEach((scroller) => {
    let hint = scroller.parentElement
    if (!hint?.classList.contains("scroll-hint")) {
      hint = document.createElement("div")
      hint.className = "scroll-hint table-wrap-hint"
      scroller.replaceWith(hint)
      hint.append(scroller)
    }

    const update = () => {
      const overflow = scroller.scrollWidth - scroller.clientWidth
      hint.classList.toggle("can-scroll-start", scroller.scrollLeft > 1)
      hint.classList.toggle("can-scroll-end", scroller.scrollLeft < overflow - 1)
      if (overflow > 1) {
        scroller.tabIndex = 0
      } else {
        scroller.removeAttribute("tabindex")
      }
    }

    update()
    scroller.addEventListener("scroll", update, { passive: true })
    globalThis.addEventListener("resize", update)
    if (globalThis.ResizeObserver) {
      new ResizeObserver(update).observe(scroller)
    }
  })

  // Code tab strips hide their scrollbar, so fade the edge that has more tabs past it.
  document.querySelectorAll(".tab-list").forEach((list) => {
    if (list.parentElement?.classList.contains("scroll-hint")) {
      return
    }

    const hint = document.createElement("div")
    hint.className = "scroll-hint tab-list-hint"
    list.replaceWith(hint)
    hint.append(list)
    const update = () => {
      const overflow = list.scrollWidth - list.clientWidth
      hint.classList.toggle("can-scroll-start", list.scrollLeft > 1)
      hint.classList.toggle("can-scroll-end", list.scrollLeft < overflow - 1)
    }

    update()
    list.addEventListener("scroll", update, { passive: true })
    if (globalThis.ResizeObserver) {
      new ResizeObserver(update).observe(list)
    }
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
        announce("Markdown copied to clipboard")
      } catch {
        label.textContent = "Copy failed"
        announce("Couldn't copy the Markdown")
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

function initializeFaq() {
  const items = document.querySelectorAll(".faq details")
  if (!items.length) {
    return
  }

  const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)")

  items.forEach((details) => {
    const summary = details.querySelector("summary")
    const body = details.querySelector(".faq-body")
    if (!summary || !body) {
      return
    }

    let animation = null

    // Clicking a question more than once in a row would otherwise select the answer's text.
    summary.addEventListener("mousedown", (event) => {
      if (event.detail > 1) {
        event.preventDefault()
      }
    })

    summary.addEventListener("click", (event) => {
      if (reducedMotion.matches || typeof body.animate !== "function") {
        return
      }

      event.preventDefault()

      // Animate only the answer, so the question text never sits inside an animating or clipped box.
      // A closed answer still reports its full size, so start a fresh open from zero.
      const from = details.open ? body.getBoundingClientRect().height : 0
      const opening = !details.open || details.classList.contains("is-closing")
      animation?.cancel()

      details.open = true
      details.classList.toggle("is-closing", !opening)
      const fullHeight = body.getBoundingClientRect().height
      const fullPadding = getComputedStyle(body).paddingBottom
      const progress = fullHeight > 0 ? from / fullHeight : 0
      const collapsed = { height: "0px", paddingBottom: "0px", opacity: 0 }
      const expanded = { height: `${fullHeight}px`, paddingBottom: fullPadding, opacity: 1 }
      const current = {
        height: `${from}px`,
        paddingBottom: `${parseFloat(fullPadding) * progress}px`,
        opacity: progress,
      }

      body.style.overflow = "hidden"
      const running = body.animate([current, opening ? expanded : collapsed], {
        duration: 260,
        easing: "cubic-bezier(0.2, 0, 0, 1)",
        fill: "forwards",
      })
      animation = running

      running.onfinish = () => {
        if (animation !== running) {
          return
        }

        if (!opening) {
          details.open = false
          details.classList.remove("is-closing")
        }

        animation = null
        running.cancel()
        body.style.overflow = ""
      }
    })
  })
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
