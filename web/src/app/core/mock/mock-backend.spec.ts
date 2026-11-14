import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../config/api-url';
import { ConsoleApi } from '../api/console.api';
import { ClinicDraft } from '../api/models';
import { mockBackendInterceptor } from './mock-backend';
import { MOCK_CATALOG } from './mock-catalog';

const draft = (slug: string): ClinicDraft => ({
  slug,
  nameAr: 'سمايل كير',
  nameEn: 'Smile Care',
  taglineAr: '',
  taglineEn: '',
  primaryColor: '#0F766E',
  accentColor: '#F59E0B',
  supportPhone: '+201000000000',
  domain: '',
  androidApplicationId: '',
  iosBundleId: '',
  logoSvg: null,
  features: {},
});

describe('mock backend', () => {
  let api: ConsoleApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([mockBackendInterceptor]))],
    });
    api = TestBed.inject(ConsoleApi);
  });

  it('rejects an empty login and signs in otherwise', async () => {
    await expect(firstValueFrom(api.login('', ''))).rejects.toMatchObject({ status: 400 });
    expect((await firstValueFrom(api.login('a@b.c', 'x'))).accessToken).toBeTruthy();
  });

  it('seeds three clinics in different colours', async () => {
    const page = await firstValueFrom(api.clinics('', '', 1));
    expect(page.total).toBe(3);
    expect(new Set(page.items.map((c) => c.primaryColor)).size).toBe(3);
    expect((await firstValueFrom(api.clinics('', 'Failed', 1))).total).toBe(1);
    expect((await firstValueFrom(api.clinics('nile', '', 1))).total).toBe(1);
  });

  it('previews config and repos, and refuses taken or invalid slugs', async () => {
    const preview = await firstValueFrom(
      api.previewClinic({ ...draft('smile-care'), features: { website: false } }),
    );
    expect(preview.repos).toEqual([
      'smile-care-backend',
      'smile-care-dashboard',
      'smile-care-mobile',
    ]);
    expect((preview.config['colors'] as { primary: string }).primary).toBe('#0F766E');
    await expect(firstValueFrom(api.previewClinic(draft('nile-dental')))).rejects.toMatchObject({
      status: 409,
    });
    await expect(firstValueFrom(api.previewClinic(draft('Bad Slug')))).rejects.toMatchObject({
      status: 400,
    });
  });

  it('refuses to enable an unavailable feature but toggles the phase 2 ones', async () => {
    // Every real feature is available now; a synthetic unavailable entry keeps the 409 path covered.
    const synthetic = {
      ...MOCK_CATALOG[0],
      key: 'futureFeature',
      available: false,
      defaultEnabled: false,
      createsRepo: null,
    };
    MOCK_CATALOG.push(synthetic);
    try {
      const [nile] = (await firstValueFrom(api.clinics('nile', '', 1))).items;
      await expect(
        firstValueFrom(api.setFeature(nile.id, 'futureFeature', true)),
      ).rejects.toMatchObject({
        status: 409,
        error: { code: 'FEATURE_NOT_AVAILABLE' },
      });
      const on = await firstValueFrom(api.setFeature(nile.id, 'reviews', true));
      expect(on.enabled).toBe(true);
      const state = await firstValueFrom(api.setFeature(nile.id, 'smsNotifications', false));
      expect(state.enabled).toBe(false);
    } finally {
      MOCK_CATALOG.splice(MOCK_CATALOG.indexOf(synthetic), 1);
    }
  });

  it('runs a dry-run job step by step', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout'] });
    try {
      const clinic = await firstValueFrom(api.createClinic(draft('smoke-test')));
      const res = await firstValueFrom(api.provision(clinic.id, true));
      expect(res.apiKey).toBeNull();
      expect(res.job.steps.length).toBe(14);
      await vi.advanceTimersByTimeAsync(20_000);
      const job = await firstValueFrom(
        TestBed.inject(HttpClient).get<{ status: string; steps: { status: string }[] }>(
          apiUrl(`/jobs/${res.job.id}`),
        ),
      );
      expect(job.status).toBe('Succeeded');
      expect(job.steps.every((s) => s.status === 'Succeeded')).toBe(true);
      expect((await firstValueFrom(api.clinic(clinic.id))).status).toBe('Draft');
    } finally {
      vi.useRealTimers();
    }
  });
});
