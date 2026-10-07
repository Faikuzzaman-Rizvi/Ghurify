/**
 * Mirrors PhoneNumber.TryParse on the server: a Bangladeshi mobile number typed in any of the
 * usual shapes (01712345678, +880 1712-345678, 8801712345678). The server normalises it to
 * E.164; this only decides whether to let the form submit.
 */
export function isBangladeshiMobile(input: string): boolean {
  if (/[^\d\s\-().+]/.test(input)) {
    return false;
  }

  let digits = input.replace(/\D/g, '');

  if (digits.startsWith('880')) {
    digits = digits.slice(3);
  }

  if (digits.startsWith('0')) {
    digits = digits.slice(1);
  }

  return /^1[3-9]\d{8}$/.test(digits);
}
