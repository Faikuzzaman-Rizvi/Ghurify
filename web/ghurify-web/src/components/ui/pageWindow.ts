/** Page numbers to show: always the first, last and neighbours of the current one. */
export function pageWindow(page: number, pages: number): (number | 'gap')[] {
  const wanted = new Set([1, pages, page - 1, page, page + 1]);
  const numbers = [...wanted].filter((n) => n >= 1 && n <= pages).sort((a, b) => a - b);
  const result: (number | 'gap')[] = [];

  for (const [index, n] of numbers.entries()) {
    const previous = numbers[index - 1];
    if (previous !== undefined && n - previous > 1) {
      result.push('gap');
    }
    result.push(n);
  }

  return result;
}
