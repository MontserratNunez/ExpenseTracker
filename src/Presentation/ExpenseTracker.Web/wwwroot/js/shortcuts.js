// Keyboard shortcuts. One listener on the document tells .NET which shortcut was pressed; .NET decides what it does.
//
//   n          new expense
//   ArrowLeft / ArrowRight   previous / next month
//   /          focus the filter or search of the page
//   Ctrl/Cmd+K open the command palette
//   Escape     close dialogs (they handle it themselves; it is also reported)
//   ?          show the list of shortcuts
//
// Plain keys are ignored while the user types in a field, while a dialog is open and while the focus is on something
// that uses the arrows itself (menus, radio groups, tabs, selects), so they never fight with normal typing or widgets.

const TYPING = 'input, textarea, select, [contenteditable=""], [contenteditable="true"]';
const OWN_ARROWS = '[role="dialog"], [role="menu"], [role="menuitem"], [role="radiogroup"], [role="tablist"], [role="listbox"], [role="combobox"], [role="slider"], input[type="radio"], select';

export function register(dotnetRef) {
  const handler = e => {
    const target = e.target instanceof Element ? e.target : document.body;
    const typing = target.matches(TYPING) || target.isContentEditable;
    const mod = (e.ctrlKey || e.metaKey) && !e.altKey;

    let key = null;
    if (mod && e.key.toLowerCase() === 'k') {
      key = 'mod+k';
    } else if (e.key === 'Escape') {
      key = 'Escape';
    } else if (!e.ctrlKey && !e.metaKey && !e.altKey) {
      if (typing || e.repeat) return;
      if (document.querySelector('.overlay')) return;          // a dialog or panel is open
      if (target.closest(OWN_ARROWS)) return;
      if (e.key === 'n' || e.key === 'N') key = 'n';
      else if (e.key === 'ArrowLeft' || e.key === 'ArrowRight' || e.key === '/' || e.key === '?') key = e.key;
    }

    if (key === null) return;
    // The browser's own use of these keys (find-as-you-type, the Ctrl+K search box) must not run.
    if (key === 'mod+k' || key === '/') e.preventDefault();
    dotnetRef.invokeMethodAsync('OnShortcut', key);
  };

  document.addEventListener('keydown', handler);
  return { dispose: () => document.removeEventListener('keydown', handler) };
}

// Moves the focus to the first thing marked with data-shortcut-focus="<name>" (the filter of a list, a search box).
// Returns false when the page has nothing to focus, so the shortcut does nothing.
export function focusTarget(name) {
  const marked = document.querySelector(`[data-shortcut-focus="${name}"]`);
  if (!marked) return false;
  const control = marked.matches('button, input, select, textarea, a[href]')
    ? marked
    : marked.querySelector('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href]');
  if (!control) return false;
  control.focus();
  return document.activeElement === control;
}
