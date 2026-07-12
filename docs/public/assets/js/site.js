import { initializeSiteSearch } from "./site-search.js"

document.querySelectorAll("[data-toggle='collapse'][data-target]").forEach((toggle) => {
  toggle.addEventListener("click", (event) => {
    event.preventDefault()

    const targetSelector = toggle.getAttribute("data-target")
    if (!targetSelector) {
      return
    }

    document.querySelectorAll(targetSelector).forEach((target) => {
      target.classList.toggle("in")
    })
  })
})

document.querySelector("[data-docs-toc-toggle]")?.addEventListener("click", () => {
  const toc = document.querySelector(".toc")
  if (!toc) {
    return
  }

  toc.classList.toggle("visible-toc")
  toc.classList.toggle("hidden-toc")
})

const searchModal = document.getElementById("site-search-modal")
if (searchModal) {
  initializeSiteSearch(searchModal)
}
