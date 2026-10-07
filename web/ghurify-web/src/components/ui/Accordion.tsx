import { useId, useState, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';

export interface AccordionItem {
  id: string;
  title: ReactNode;
  /** Shown at the right of the row, e.g. a difficulty badge. */
  aside?: ReactNode;
  content: ReactNode;
}

/**
 * Collapsible rows built on real buttons (aria-expanded / aria-controls), so they work by
 * keyboard and screen reader. With `toggleAllLabels`, a control above opens or closes every
 * row at once, which is how most people read an itinerary end to end.
 */
export function Accordion({
  items,
  defaultOpen = [],
  toggleAllLabels,
}: {
  items: AccordionItem[];
  defaultOpen?: string[];
  toggleAllLabels?: { expand: string; collapse: string };
}) {
  const baseId = useId();
  const [open, setOpen] = useState<ReadonlySet<string>>(() => new Set(defaultOpen));
  const allOpen = items.length > 0 && items.every((item) => open.has(item.id));

  function toggle(id: string) {
    setOpen((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <div>
      {toggleAllLabels && items.length > 1 && (
        <div className="mb-3 flex justify-end">
          <button
            type="button"
            onClick={() => setOpen(allOpen ? new Set() : new Set(items.map((item) => item.id)))}
            className="text-sm font-medium text-hill underline-offset-4 hover:underline"
          >
            {allOpen ? toggleAllLabels.collapse : toggleAllLabels.expand}
          </button>
        </div>
      )}

      <ul className="flex flex-col gap-3">
        {items.map((item) => {
          const isOpen = open.has(item.id);
          const buttonId = `${baseId}-${item.id}-button`;
          const panelId = `${baseId}-${item.id}-panel`;

          return (
            <li
              key={item.id}
              className={`overflow-hidden rounded-xl bg-mist transition-shadow ${
                isOpen ? 'bg-white shadow-md ring-1 ring-hill/10' : ''
              }`}
            >
              <h3 className="text-base!">
                <button
                  id={buttonId}
                  type="button"
                  aria-expanded={isOpen}
                  aria-controls={panelId}
                  onClick={() => toggle(item.id)}
                  className="flex w-full items-center gap-3 px-5 py-4 text-left font-display text-base font-semibold text-deep"
                >
                  <span className="flex-1">{item.title}</span>
                  {item.aside}
                  <ChevronDown
                    aria-hidden="true"
                    className={`h-5 w-5 shrink-0 text-hill transition-transform duration-300 ${
                      isOpen ? 'rotate-180' : ''
                    }`}
                  />
                </button>
              </h3>
              <div
                id={panelId}
                role="region"
                aria-labelledby={buttonId}
                hidden={!isOpen}
                className="animate-fade-in px-5 pb-5"
              >
                {item.content}
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
