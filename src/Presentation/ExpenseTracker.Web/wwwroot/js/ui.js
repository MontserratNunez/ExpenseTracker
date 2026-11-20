// Small helpers for the design-system overlays. Loaded on demand as an ES module by UiInterop.cs.

const FOCUSABLE =
  'a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),' +
  'select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

const traps = new Map();
const menus = new Map();
let nextId = 1;
let scrollLocks = 0;
let escapeListener = null;

function visibleFocusables(root) {
  return [...root.querySelectorAll(FOCUSABLE)].filter(e => e.offsetParent !== null || e === document.activeElement);
}

// Keeps Tab and Shift+Tab inside `el`, moves focus into it, and remembers who had the focus.
// Returns an id to pass to releaseFocus. Focus goes to [data-autofocus], else the first form field
// of the body, else the first focusable element.
export function trapFocus(el, dotnetRef) {
  const previous = document.activeElement;

  const onKeyDown = e => {
    if (e.key !== 'Tab') return;

    const items = visibleFocusables(el);
    if (items.length === 0) { e.preventDefault(); el.focus(); return; }

    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;

    if (e.shiftKey && (active === first || active === el)) { last.focus(); e.preventDefault(); }
    else if (!e.shiftKey && (active === last || active === el)) { first.focus(); e.preventDefault(); }
  };

  el.addEventListener('keydown', onKeyDown);

  const target =
    el.querySelector('[data-autofocus]') ??
    el.querySelector('.overlay__body input:not([disabled]):not([type="hidden"]),' +
                     '.overlay__body select:not([disabled]),.overlay__body textarea:not([disabled])') ??
    visibleFocusables(el)[0] ??
    el;
  target.focus();

  const id = nextId++;
  traps.set(id, { el, onKeyDown, previous, dotnetRef });
  ensureEscapeListener();
  return id;
}

// Esc normally reaches the overlay through its own keydown handler, which needs the focus to be inside it.
// This backup covers the short moment before the focus moves in (or any time it is elsewhere): when the focus
// is outside the top-most overlay, Esc is sent to it. With the focus inside, the overlay handles it, so it never fires twice.
function ensureEscapeListener() {
  if (escapeListener) return;

  escapeListener = e => {
    if (e.key !== 'Escape' || e.defaultPrevented) return;
    const top = [...traps.values()].pop();
    if (!top || top.el.contains(e.target)) return;
    top.dotnetRef?.invokeMethodAsync('OnEscape');
  };
  document.addEventListener('keydown', escapeListener);
}

// Stops the trap and gives the focus back to the element that had it before (if it still exists).
export function releaseFocus(id) {
  const trap = traps.get(id);
  if (!trap) return;

  trap.el.removeEventListener('keydown', trap.onKeyDown);
  traps.delete(id);
  if (traps.size === 0 && escapeListener) {
    document.removeEventListener('keydown', escapeListener);
    escapeListener = null;
  }

  const previous = trap.previous;
  if (previous && previous.isConnected && typeof previous.focus === 'function') previous.focus();
}

// Prevents the page behind an overlay from scrolling. Counted, so stacked overlays work.
export function lockScroll(lock) {
  scrollLocks = Math.max(0, scrollLocks + (lock ? 1 : -1));
  document.body.style.overflow = scrollLocks > 0 ? 'hidden' : '';
}

// ----- Dropdown menu keyboard support (WAI-ARIA "menu button" pattern) -----

export function attachMenu(root, dotnetRef) {
  const onKeyDown = e => {
    const trigger = root.querySelector('.dropdown-app__trigger');
    const menu = root.querySelector('[role="menu"]');

    // Closed menu: arrows on the button open it.
    if (!menu && e.target === trigger && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      e.preventDefault();
      dotnetRef.invokeMethodAsync('OpenFromKeyboard', e.key === 'ArrowUp' ? 'last' : 'first');
      return;
    }

    if (!menu || !menu.contains(e.target)) return;

    const items = [...menu.querySelectorAll('[role="menuitem"]:not([disabled])')];
    const index = items.indexOf(document.activeElement);

    switch (e.key) {
      case 'ArrowDown': e.preventDefault(); items[(index + 1) % items.length]?.focus(); break;
      case 'ArrowUp': e.preventDefault(); items[(index - 1 + items.length) % items.length]?.focus(); break;
      case 'Home': e.preventDefault(); items[0]?.focus(); break;
      case 'End': e.preventDefault(); items[items.length - 1]?.focus(); break;
      case 'Escape':
        // Only the menu closes, not an overlay the menu may be inside of.
        e.preventDefault();
        e.stopPropagation();
        dotnetRef.invokeMethodAsync('CloseFromKeyboard', true);
        break;
      case 'Tab': dotnetRef.invokeMethodAsync('CloseFromKeyboard', false); break;
    }
  };

  root.addEventListener('keydown', onKeyDown);
  const id = nextId++;
  menus.set(id, { root, onKeyDown });
  return id;
}

export function detachMenu(id) {
  const menu = menus.get(id);
  if (!menu) return;
  menu.root.removeEventListener('keydown', menu.onKeyDown);
  menus.delete(id);
}

export function focusMenuItem(root, which) {
  const items = [...root.querySelectorAll('[role="menuitem"]:not([disabled])')];
  (which === 'last' ? items[items.length - 1] : items[0])?.focus();
}

export function focusMenuTrigger(root) {
  root.querySelector('.dropdown-app__trigger')?.focus();
}
