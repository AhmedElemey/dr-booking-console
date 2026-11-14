import ar from '../../../../public/i18n/ar.json';
import en from '../../../../public/i18n/en.json';
import { flatten } from '../../../testing/test-providers';
import { KNOWN_ERROR_CODES } from '../http/error-codes';
import { fillPlatform } from './platform-loader';

describe('translations', () => {
  it('have the same keys in Arabic and English', () => {
    expect(flatten(ar).sort()).toEqual(flatten(en).sort());
  });

  it('have a message for every known API error code', () => {
    const keys = flatten(en);
    for (const code of KNOWN_ERROR_CODES) expect(keys).toContain(`errors.${code}`);
    expect(keys).toContain('errors.UNKNOWN');
  });

  it('use the same interpolation placeholders in both languages', () => {
    const values = (obj: object, prefix = ''): Record<string, string> =>
      Object.fromEntries(
        Object.entries(obj).flatMap(([k, v]) =>
          typeof v === 'object' && v !== null
            ? Object.entries(values(v, `${prefix}${k}.`))
            : [[`${prefix}${k}`, String(v)]],
        ),
      );
    const placeholders = (text: string) => (text.match(/\{\{\w+\}\}/g) ?? []).sort().join(',');
    const arValues = values(ar);
    for (const [key, text] of Object.entries(values(en))) {
      expect(placeholders(arValues[key]), key).toEqual(placeholders(text));
    }
  });

  it('fills the platform name wherever {{platform}} appears', () => {
    const filled = JSON.stringify(fillPlatform(en, 'Dr Booking'));
    expect(filled).toContain('Dr Booking');
    expect(filled).not.toContain('{{platform}}');
    expect(JSON.stringify(fillPlatform(ar, 'Dr Booking'))).toContain('Dr Booking');
  });
});
