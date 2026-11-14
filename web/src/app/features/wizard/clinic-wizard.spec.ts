import { TestBed } from '@angular/core/testing';

import { provideMockApi, provideTestTranslations } from '../../../testing/test-providers';
import { ClinicWizard, defaultAppId, slugify } from './clinic-wizard';

describe('wizard helpers', () => {
  it('derives a slug from an English name', () => {
    expect(slugify('Smile Care')).toBe('smile-care');
    expect(slugify('  Dr. Ahmed & Sons Dental!! ')).toBe('dr-ahmed-sons-dental');
    expect(slugify('A'.repeat(60)).length).toBe(40);
    expect(slugify('عيادة')).toBe('');
  });

  it('derives the app id from the slug', () => {
    expect(defaultAppId('smile-care')).toBe('com.smilecare.app');
    expect(defaultAppId('24-hours')).toBe('com.c24hours.app');
    expect(defaultAppId('')).toBe('');
  });
});

describe('ClinicWizard', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [...provideMockApi(), ...provideTestTranslations('en')],
    });
    const fixture = TestBed.createComponent(ClinicWizard);
    fixture.detectChanges();
    return fixture;
  }

  it('fills the slug and app ids from the English name until they are edited', () => {
    const fixture = setup();
    const form = (fixture.componentInstance as unknown as { identity: ClinicWizard['identity'] })
      .identity;
    form.controls.nameEn.setValue('Smile Care');
    expect(form.controls.slug.value).toBe('smile-care');
    expect(form.controls.androidApplicationId.value).toBe('com.smilecare.app');
    expect(form.controls.iosBundleId.value).toBe('com.smilecare.app');

    (fixture.componentInstance as unknown as { markSlugEdited(): void }).markSlugEdited();
    form.controls.nameEn.setValue('Smile Care Plus');
    expect(form.controls.slug.value).toBe('smile-care');
  });

  it('lists the catalog with coming-soon features disabled and repo-creating ones marked', async () => {
    const fixture = setup();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.feature-row').length).toBe(21);
    expect(el.querySelectorAll('.feature-row.soon').length).toBe(13);
    expect(el.querySelectorAll('.badge-repo').length).toBe(2);
    expect(el.querySelectorAll('.badge-soon').length).toBe(13);
    const draft = (
      fixture.componentInstance as unknown as { draft(): { features: Record<string, boolean> } }
    ).draft();
    expect(draft.features['smsNotifications']).toBe(false);
    expect(draft.features['mobileApp']).toBe(true);
    expect(draft.features['onlinePayments']).toBe(false);
  });
});
