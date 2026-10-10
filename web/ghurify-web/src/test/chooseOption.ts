import { waitFor } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';

/**
 * Picks a value in one of the app's dropdowns (components/ui/Select) the way a person does:
 * opens it, then clicks the option. The stand-in for `user.selectOptions`, which only works on
 * the browser's own <select>, which the app no longer uses. `value` is the option's value, so a
 * test reads the same whichever language the labels are in.
 *
 * Options loaded over the network (the destinations) may arrive after the list opens, so it
 * waits for the option rather than failing on the first look.
 */
export async function chooseOption(user: UserEvent, control: HTMLElement, value: string) {
  await user.click(control);
  const option = await waitFor(() => {
    const listId = control.getAttribute('aria-controls');
    const found = (listId ? document.getElementById(listId) : null)?.querySelector<HTMLElement>(
      `[role="option"][data-value="${value}"]`,
    );
    if (!found) throw new Error(`No option with the value "${value}" in the opened list.`);
    return found;
  });
  await user.click(option);
}
