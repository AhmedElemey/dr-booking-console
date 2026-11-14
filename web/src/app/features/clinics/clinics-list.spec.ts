import { TestBed } from '@angular/core/testing';

import { provideMockApi, provideTestTranslations } from '../../../testing/test-providers';
import { resetMockDb } from '../../core/mock/mock-backend';
import { ClinicsList } from './clinics-list';

describe('ClinicsList', () => {
  beforeEach(() => resetMockDb());

  it('shows each clinic with its status, brand colour and feature count', async () => {
    TestBed.configureTestingModule({
      providers: [...provideMockApi(), ...provideTestTranslations('en')],
    });
    const fixture = TestBed.createComponent(ClinicsList);
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r, 700));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const cards = Array.from(el.querySelectorAll('.clinic-card'));
    expect(cards.length).toBe(3);
    const text = el.textContent ?? '';
    for (const name of ['Nile Dental', 'Sunrise Clinic', 'Palm Orthodontics'])
      expect(text).toContain(name);
    expect(text).toContain('Failed');
    expect(text).toContain('Suspended');
    const swatches = Array.from(el.querySelectorAll<HTMLElement>('.clinic-swatch')).map(
      (s) => s.style.background,
    );
    expect(new Set(swatches).size).toBe(3);
  });
});
