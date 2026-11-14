const HEX = /^#[0-9a-f]{6}$/i;

export function isHexColor(value: string | null | undefined): value is string {
  return !!value && HEX.test(value);
}

/** WCAG relative luminance of a `#RRGGBB` colour. */
export function luminance(hex: string): number {
  const channel = (i: number) => {
    const c = parseInt(hex.slice(1 + i * 2, 3 + i * 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(0) + 0.7152 * channel(1) + 0.0722 * channel(2);
}

/** White or near-black, whichever reads better on `hex`. */
export function contrastText(hex: string): string {
  return isHexColor(hex) && luminance(hex) > 0.42 ? '#111827' : '#ffffff';
}

/** Falls back to a neutral grey while the user is still typing an invalid colour. */
export function safeColor(hex: string | null | undefined, fallback = '#9ca3af'): string {
  return isHexColor(hex) ? hex : fallback;
}
