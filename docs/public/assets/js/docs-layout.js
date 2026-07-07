document.querySelector("[data-docs-toc-toggle]")?.addEventListener("click", () => {
  const toc = document.querySelector(".toc")
  if (!toc) {
    return
  }

  toc.classList.toggle("visible-toc")
  toc.classList.toggle("hidden-toc")
})
