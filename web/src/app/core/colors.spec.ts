import { contrastText, isHexColor, safeColor } from './colors';

describe('colors', () => {
  it('validates #RRGGBB only', () => {
    expect(isHexColor('#0F766E')).toBe(true);
    expect(isHexColor('#0f766e')).toBe(true);
    expect(isHexColor('0F766E')).toBe(false);
    expect(isHexColor('#fff')).toBe(false);
    expect(isHexColor(null)).toBe(false);
  });

  it('picks readable text for light and dark backgrounds', () => {
    expect(contrastText('#FFFFFF')).toBe('#111827');
    expect(contrastText('#F59E0B')).toBe('#111827');
    expect(contrastText('#0F766E')).toBe('#ffffff');
    expect(contrastText('#3F3CCF')).toBe('#ffffff');
  });

  it('falls back to grey while a colour is being typed', () => {
    expect(safeColor('#12')).toBe('#9ca3af');
    expect(safeColor('#123456')).toBe('#123456');
  });
});
