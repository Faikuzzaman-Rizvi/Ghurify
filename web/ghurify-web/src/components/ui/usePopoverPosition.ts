import { useLayoutEffect, useRef, type RefObject } from 'react';

/** The gap between the control and the panel, and the smallest gap to the window's edge. */
const gap = 8;
const edge = 8;
/** Below this a flipped panel is worse than a scrolling one. */
const leastHeight = 180;

export interface PopoverOptions {
  /** Which edge of the panel lines up with the control's. */
  align?: 'left' | 'right';
  /** Make the panel at least as wide as the control. */
  matchWidth?: boolean;
  /** The tallest the panel may be when there is room for it. */
  maxHeight?: number;
}

/** `will-change` values that make an element the containing block of its fixed children. */
const containingWillChange = /transform|perspective|filter|backdrop-filter|contain/;

/**
 * The browser measures a `fixed` element against the window, unless an ancestor establishes a
 * containing block for it, when it measures against that ancestor instead. A transform (an
 * animated bottom sheet) does it, and so does a `backdrop-filter` (the frosted search bar on
 * the admin lists): missing that one placed the status list on the payments page a whole
 * search bar's width to the right of its button, and past the window's edge. Finding the
 * ancestor lets the arithmetic below hold in every case.
 */
function containingBlock(element: HTMLElement): HTMLElement | null {
  for (let node = element.parentElement; node; node = node.parentElement) {
    const style = getComputedStyle(node);
    const backdrop =
      style.backdropFilter ||
      (style as CSSStyleDeclaration & { webkitBackdropFilter?: string }).webkitBackdropFilter;
    if (
      style.transform !== 'none' ||
      (style.translate && style.translate !== 'none') ||
      (style.scale && style.scale !== 'none') ||
      (style.rotate && style.rotate !== 'none') ||
      style.perspective !== 'none' ||
      style.filter !== 'none' ||
      (backdrop && backdrop !== 'none') ||
      containingWillChange.test(style.willChange) ||
      /paint|layout|strict|content/.test(style.contain) ||
      (style.containerType && style.containerType !== 'normal')
    ) {
      return node;
    }
  }
  return null;
}

/** Writes only what changed, so a ResizeObserver watching the panel settles instead of looping. */
function put(
  style: CSSStyleDeclaration,
  property: 'top' | 'left' | 'maxHeight' | 'minWidth',
  value: string,
) {
  if (style[property] !== value) style[property] = value;
}

/**
 * Places a panel — a listbox, a calendar — beside the control that opens it. Put the two refs
 * it hands back on the control and on the panel, and give the panel `position: fixed`.
 *
 * Laid out in the normal flow, such a panel is cut off at the edge of any box that scrolls
 * (the filter sidebar, a modal) and lengthens that box as it opens, which is where a stray
 * scrollbar comes from. Measured against the window instead, it is never clipped and adds to
 * nothing's scroll height: it flips above the control when there is not enough room below —
 * so it never covers a dialog's buttons — is kept inside the window from side to side, and
 * gets a height that fits whatever room is left.
 *
 * The position is written straight to the panel's style rather than held in state: it changes
 * on every scroll frame and nothing else on the page depends on it. Both elements are
 * measured, so the panel has to be in the document while it is open rather than hidden with
 * an attribute.
 */
export function usePopoverPosition<Anchor extends HTMLElement, Panel extends HTMLElement>(
  open: boolean,
  { align = 'left', matchWidth = true, maxHeight = 320 }: PopoverOptions = {},
): { anchorRef: RefObject<Anchor | null>; panelRef: RefObject<Panel | null> } {
  const anchorRef = useRef<Anchor | null>(null);
  const panelRef = useRef<Panel | null>(null);

  useLayoutEffect(() => {
    if (!open) return;
    const control = anchorRef.current;
    const box = panelRef.current;
    if (!control || !box) return;

    function place() {
      if (!control || !box) return;
      const rect = control.getBoundingClientRect();
      const roomBelow = window.innerHeight - rect.bottom - gap - edge;
      const roomAbove = rect.top - gap - edge;
      // The panel's own height, before the clamp below, so one that fits is never flipped
      // for nothing.
      const wanted = Math.min(maxHeight, box.scrollHeight);
      const dropUp = wanted > roomBelow && roomAbove > roomBelow && roomAbove >= leastHeight;
      const height = Math.min(maxHeight, Math.max(leastHeight, dropUp ? roomAbove : roomBelow));

      const width = box.offsetWidth;
      const left = Math.min(
        Math.max(edge, align === 'right' ? rect.right - width : rect.left),
        Math.max(edge, window.innerWidth - width - edge),
      );
      const top = dropUp
        ? Math.max(edge, rect.top - gap - Math.min(height, wanted))
        : rect.bottom + gap;

      const origin = containingBlock(control)?.getBoundingClientRect();
      put(box.style, 'top', `${Math.round(top - (origin?.top ?? 0))}px`);
      put(box.style, 'left', `${Math.round(left - (origin?.left ?? 0))}px`);
      put(box.style, 'maxHeight', `${Math.round(height)}px`);
      if (matchWidth) put(box.style, 'minWidth', `${Math.round(rect.width)}px`);
      box.dataset['drop'] = dropUp ? 'up' : 'down';
    }

    place();

    // Scrolling anywhere, in any ancestor, moves the control: `true` catches them all.
    window.addEventListener('scroll', place, true);
    window.addEventListener('resize', place);
    // The calendar changes height when it steps out to months or years.
    const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(place) : null;
    observer?.observe(box);

    return () => {
      window.removeEventListener('scroll', place, true);
      window.removeEventListener('resize', place);
      observer?.disconnect();
    };
  }, [open, align, matchWidth, maxHeight]);

  return { anchorRef, panelRef };
}
