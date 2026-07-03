const stepList = document.querySelector("#installSteps");
const loaderTabs = document.querySelector(".loader-tabs");
const loaderButtons = [...document.querySelectorAll("[data-loader]")];
const screenshotSources = {
  "menu-screen": "assets/mainmenu.png",
  "hud-screen": "assets/ingame.png",
  "credits-screen": "assets/credits.png"
};
let isInstallVisible = false;
let hasQueuedStepReveal = false;
let hasPlayedStepReveal = false;
let textSwitchTimer = 0;
let lightboxParts = null;
let lastLightboxTrigger = null;

if ("scrollRestoration" in history) {
  history.scrollRestoration = "manual";
}

window.addEventListener("pageshow", () => {
  const navigation = performance.getEntriesByType("navigation")[0];
  if (!location.hash || navigation?.type === "reload") {
    requestAnimationFrame(() => window.scrollTo(0, 0));
  }
});

const installSteps = {
  melonloader: [
    {
      type: "note",
      parts: [
        { text: "If on Mac, there is an " },
        { text: "auto installer", href: "https://github.com/sbrothers7/UMMInstall/releases/latest" },
        { text: " for your convenience." }
      ]
    },
    {
      type: "step",
      parts: [
        { text: "Download " },
        { text: "modlist.org app", href: "https://github.com/modlist-org/modlist_org_app/releases/latest" },
        { text: " and " },
        { text: "Quartz", href: "https://github.com/QuartzTeam/Quartz/releases/latest" },
        { text: "." }
      ]
    },
    { type: "step", text: "If not installed MelonLoader yet, install it using the modlist.org app." },
    {
      type: "note",
      text: "If on Mac, In the modlist.org app, press \"Copy Native Launch Options\" in the \"Installed\" tab and paste it into your Steam launch arguments."
    },
    { type: "step", text: "Press \"Install Mod From File\" then select the zip (Quartz.zip)." },
    { type: "step", text: "Done!" }
  ],
  unitymodmanager: [
    { type: "step", marker: "0", text: "First make sure you have UnityModManager set up for ADoFaI." },
    {
      type: "step",
      parts: [
        { text: "Download " },
        { text: "QuartzUmm.zip", code: true },
        { text: " from " },
        { text: "releases", href: "https://github.com/QuartzTeam/Quartz/releases/latest" },
        { text: "." }
      ]
    },
    {
      type: "step",
      parts: [
        { text: "In the UMM installer, use \"Install mod\" and pick " },
        { text: "QuartzUmm.zip", code: true },
        { text: " - or just simply drag the " },
        { text: "QuartzUmm.zip", code: true },
        { text: " into the drag zip box" }
      ]
    },
    { type: "step", text: "Done! Open the in-game menu with the mod's keybind (settings live there, not in the UMM panel)." }
  ]
};

function getInstallSteps(loader) {
  return installSteps[loader] || installSteps.melonloader;
}

function renderInline(target, step) {
  const parts = Array.isArray(step.parts) ? step.parts : [{ text: typeof step.text === "string" ? step.text : "" }];
  parts.forEach((part) => {
    const text = typeof part.text === "string" ? part.text : "";
    const href = typeof part.href === "string" && part.href.startsWith("https://") ? part.href : "";
    let node = document.createTextNode(text);
    if (part.code) {
      node = document.createElement("code");
      node.textContent = text;
    } else if (href) {
      node = document.createElement("a");
      node.href = href;
      node.target = "_blank";
      node.rel = "noopener noreferrer";
      node.textContent = text;
    }
    target.append(node);
  });
}

function openExternalLinksInNewTabs(root = document) {
  root.querySelectorAll('a[href^="http://"], a[href^="https://"]').forEach((link) => {
    link.target = "_blank";
    link.rel = "noopener noreferrer";
  });
}

let stepHeightCleanup = null;

function animateStepListHeight(previousHeight) {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  const newHeight = stepList.getBoundingClientRect().height;
  if (Math.abs(newHeight - previousHeight) < 1) return;
  if (stepHeightCleanup) stepHeightCleanup();
  let failsafeTimer = 0;
  const finish = () => {
    clearTimeout(failsafeTimer);
    stepList.style.height = "";
    stepList.style.overflow = "";
    stepList.style.transition = "";
    stepList.removeEventListener("transitionend", onEnd);
    stepHeightCleanup = null;
  };
  const onEnd = (event) => {
    if (event.target === stepList && event.propertyName === "height") finish();
  };
  stepHeightCleanup = finish;
  stepList.style.overflow = "hidden";
  stepList.style.transition = "none";
  stepList.style.height = `${previousHeight}px`;
  void stepList.offsetHeight;
  stepList.style.transition = "height 360ms cubic-bezier(0.22, 1, 0.36, 1)";
  stepList.style.height = `${newHeight}px`;
  stepList.addEventListener("transitionend", onEnd);
  failsafeTimer = window.setTimeout(finish, 600);
}

function renderSteps(steps) {
  const previousHeight = stepList.getBoundingClientRect().height;
  if (hasPlayedStepReveal) {
    stepList.classList.add("is-static");
    stepList.classList.remove("is-text-switching");
    clearTimeout(textSwitchTimer);
  } else {
    stepList.classList.remove("is-revealing", "is-static");
  }
  let stepNumber = 1;
  stepList.replaceChildren(
    ...steps.map((entry, index) => {
      const step = typeof entry === "object" && entry ? entry : { type: "step", text: String(entry || "") };
      const isCallout = step.type === "note" || step.type === "warning";
      const item = document.createElement("li");
      const copy = document.createElement("span");
      copy.className = "step-copy";
      if (isCallout) {
        const title = document.createElement("strong");
        const body = document.createElement("span");
        item.className = `step-note${step.type === "warning" ? " is-warning" : ""}`;
        body.className = "step-note-body";
        title.className = "step-note-title";
        title.textContent = step.type === "warning" ? "Warning" : "Note";
        renderInline(body, step);
        copy.append(title, body);
      } else {
        item.dataset.marker = step.marker || String(stepNumber);
        renderInline(copy, step);
        if (!step.marker) stepNumber += 1;
      }
      item.append(copy);
      item.style.setProperty("--step-index", index);
      return item;
    })
  );
  if (hasPlayedStepReveal) {
    stepList.classList.remove("is-switching");
    void stepList.offsetWidth;
    animateStepListHeight(previousHeight);
    stepList.classList.add("is-text-switching");
    textSwitchTimer = window.setTimeout(() => stepList.classList.remove("is-text-switching"), 1800);
    return;
  }
  queueStepReveal();
}

function revealQueuedSteps() {
  if (!hasQueuedStepReveal || !isInstallVisible) return;
  hasQueuedStepReveal = false;
  requestAnimationFrame(() => {
    if (!isInstallVisible) {
      hasQueuedStepReveal = true;
      return;
    }
    hasPlayedStepReveal = true;
    stepList.classList.remove("is-switching", "is-static");
    void stepList.offsetWidth;
    stepList.classList.add("is-revealing");
  });
}

function queueStepReveal() {
  hasQueuedStepReveal = true;
  revealQueuedSteps();
}

function moveLoaderThumb(button) {
  if (!loaderTabs || !button) return;
  const tabsBox = loaderTabs.getBoundingClientRect();
  const buttonBox = button.getBoundingClientRect();
  loaderTabs.style.setProperty("--tab-thumb-width", `${buttonBox.width}px`);
  loaderTabs.style.setProperty("--tab-thumb-x", `${buttonBox.left - tabsBox.left}px`);
}

function setActiveLoader(loader) {
  let activeButton = null;
  loaderButtons.forEach((button) => {
    const isActive = button.dataset.loader === loader;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-selected", String(isActive));
    if (isActive) activeButton = button;
  });
  moveLoaderThumb(activeButton);
}

function loadSteps(loader) {
  setActiveLoader(loader);
  if (!hasPlayedStepReveal) stepList.classList.add("is-switching");
  renderSteps(getInstallSteps(loader));
}

function watchStepVisibility() {
  if (!("IntersectionObserver" in window)) {
    isInstallVisible = true;
    revealQueuedSteps();
    return;
  }

  const observer = new IntersectionObserver(
    ([entry]) => {
      isInstallVisible = entry.isIntersecting && entry.intersectionRatio >= 0.28;
      revealQueuedSteps();
    },
    { threshold: [0, 0.28, 0.55] }
  );
  observer.observe(stepList);
}

function getScreenshotSource(screen) {
  const sourceClass = Object.keys(screenshotSources).find((className) => screen.classList.contains(className));
  return sourceClass ? screenshotSources[sourceClass] : "";
}

function getScreenshotCaption(screen) {
  return screen.closest("figure")?.querySelector("figcaption")?.textContent.trim() || "Quartz screenshot";
}

function createScreenshotLightbox() {
  const root = document.createElement("div");
  const closeButton = document.createElement("button");
  const frame = document.createElement("figure");
  const image = document.createElement("img");
  const caption = document.createElement("figcaption");

  root.className = "shot-lightbox";
  root.hidden = true;
  root.setAttribute("role", "dialog");
  root.setAttribute("aria-modal", "true");
  root.setAttribute("aria-label", "Screenshot preview");
  closeButton.className = "lightbox-close";
  closeButton.type = "button";
  closeButton.textContent = "Close";
  frame.className = "lightbox-frame";
  image.className = "lightbox-image";
  caption.className = "lightbox-caption";

  frame.append(image, caption);
  root.append(closeButton, frame);
  document.body.append(root);

  root.addEventListener("click", (event) => {
    if (event.target === root) closeScreenshotLightbox();
  });
  closeButton.addEventListener("click", closeScreenshotLightbox);
  return { root, closeButton, image, caption };
}

function openScreenshotLightbox(screen) {
  const src = getScreenshotSource(screen);
  if (!src) return;
  const caption = getScreenshotCaption(screen);
  if (!lightboxParts) lightboxParts = createScreenshotLightbox();
  lastLightboxTrigger = screen;
  lightboxParts.image.src = src;
  lightboxParts.image.alt = caption;
  lightboxParts.caption.textContent = caption;
  lightboxParts.root.hidden = false;
  document.body.classList.add("has-lightbox");
  lightboxParts.closeButton.focus();
}

function closeScreenshotLightbox() {
  if (!lightboxParts || lightboxParts.root.hidden) return;
  lightboxParts.root.hidden = true;
  document.body.classList.remove("has-lightbox");
  lastLightboxTrigger?.focus();
}

function setupScreenshotLightbox() {
  document.querySelectorAll(".mock-screen").forEach((screen) => {
    const caption = getScreenshotCaption(screen);
    screen.tabIndex = 0;
    screen.setAttribute("role", "button");
    screen.setAttribute("aria-label", `View ${caption} fullscreen`);
    screen.addEventListener("click", () => openScreenshotLightbox(screen));
    screen.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") return;
      event.preventDefault();
      openScreenshotLightbox(screen);
    });
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeScreenshotLightbox();
  });
}

loaderButtons.forEach((button) => {
  button.addEventListener("click", () => loadSteps(button.dataset.loader));
});

window.addEventListener("resize", () => {
  moveLoaderThumb(document.querySelector(".loader-tabs button.active"));
});

if (document.fonts) {
  document.fonts.ready.then(() => moveLoaderThumb(document.querySelector(".loader-tabs button.active")));
}

setActiveLoader("melonloader");
openExternalLinksInNewTabs();
setupScreenshotLightbox();
watchStepVisibility();
loadSteps("melonloader");
