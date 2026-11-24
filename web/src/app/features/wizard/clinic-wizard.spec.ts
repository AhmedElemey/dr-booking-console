import { TestBed } from '@angular/core/testing';

import { FeatureDefinition } from '../../core/api/models';
import { MOCK_CATALOG } from '../../core/mock/mock-catalog';
import { provideMockApi, provideTestTranslations } from '../../../testing/test-providers';
import { ANDROID_APP_ID_PATTERN, ClinicWizard, defaultAppId, slugify } from './clinic-wizard';

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

const UNAVAILABLE: FeatureDefinition = {
  key: 'futureFeature',
  nameAr: 'ميزة قادمة',
  nameEn: 'Future feature',
  descriptionAr: 'وصف',
  descriptionEn: 'Description',
  group: 'Growth',
  available: false,
  defaultEnabled: false,
  createsRepo: null,
};

describe('Android application id', () => {
  it('has no underscore, so it can be a URL scheme', () => {
    for (const ok of ['com.smilecare.app', 'com.c24hours.app', 'eg.clinic2.patient.app']) {
      expect(ANDROID_APP_ID_PATTERN.test(ok)).toBe(true);
    }
    for (const bad of [
      'com.my_clinic.app',
      'com.clinic.app_2',
      'Com.Clinic.App',
      'single',
      '1com.a.b',
    ]) {
      expect(ANDROID_APP_ID_PATTERN.test(bad)).toBe(false);
    }
  });

  it('is derived from the slug without underscores', () => {
    expect(defaultAppId('smile-care')).not.toContain('_');
  });
});

describe('ClinicWizard', () => {
  // Every real feature is available now; a synthetic unavailable entry keeps the coming-soon path covered.
  beforeEach(() => MOCK_CATALOG.push(UNAVAILABLE));
  afterEach(() => MOCK_CATALOG.splice(MOCK_CATALOG.indexOf(UNAVAILABLE), 1));

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

  it('rejects an underscore in the Android id but keeps the iOS id rule', () => {
    const fixture = setup();
    const form = (fixture.componentInstance as unknown as { identity: ClinicWizard['identity'] })
      .identity;
    form.controls.androidApplicationId.setValue('com.my_clinic.app');
    expect(form.controls.androidApplicationId.valid).toBe(false);
    expect(form.controls.androidApplicationId.errors).toHaveProperty('pattern');
    form.controls.androidApplicationId.setValue('com.myclinic.app');
    expect(form.controls.androidApplicationId.valid).toBe(true);
    form.controls.iosBundleId.setValue('com.my_clinic.app');
    expect(form.controls.iosBundleId.valid).toBe(true);
  });

  it('lists the catalog with unavailable features disabled and repo-creating ones marked', async () => {
    const fixture = setup();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.feature-row').length).toBe(25);
    expect(el.querySelectorAll('.feature-row.soon').length).toBe(1);
    expect(el.querySelectorAll('.badge-repo').length).toBe(2);
    expect(el.querySelectorAll('.badge-soon').length).toBe(1);
    const draft = (
      fixture.componentInstance as unknown as { draft(): { features: Record<string, boolean> } }
    ).draft();
    expect(draft.features['smsNotifications']).toBe(false);
    expect(draft.features['mobileApp']).toBe(true);
    expect(draft.features['onlinePayments']).toBe(false);
    expect(draft.features['futureFeature']).toBe(false);
    const phase2 = [
      'onlinePayments',
      'whatsappReminders',
      'waitingList',
      'liveQueue',
      'manualReview',
      'publicPrices',
      'holidayCalendar',
      'workingHoursTemplates',
      'patientRecords',
      'reviews',
      'reports',
      'promotions',
      'recallCampaigns',
      'videoConsultation',
      'familyAccounts',
      'insurance',
    ];
    for (const k of phase2) expect(draft.features[k], k).toBe(false);
  });

  it('shows the setup hint only for features that need provider keys', async () => {
    const fixture = setup();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const notes = Array.from(el.querySelectorAll('.setup-note')).map((n) => n.textContent ?? '');
    expect(notes.length).toBe(3);
    expect(notes.some((n) => n.includes('Payments:Provider=Paymob'))).toBe(true);
    expect(notes.some((n) => n.includes('WhatsApp:Provider=Meta'))).toBe(true);
    expect(notes.some((n) => n.includes('Jitsi'))).toBe(true);
  });
});
