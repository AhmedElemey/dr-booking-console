import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { Observable, delay, of, throwError } from 'rxjs';

import { API_ROOT } from '../config/api-url';
import {
  Admin,
  AuditEntry,
  Clinic,
  ClinicDraft,
  ClinicStatus,
  Job,
  JobStep,
  RepoKind,
} from '../api/models';
import { MOCK_CATALOG } from './mock-catalog';

interface Db {
  admins: Admin[];
  clinics: Clinic[];
  jobs: Job[];
  audit: AuditEntry[];
  me: Admin;
}

const AVAILABLE = MOCK_CATALOG.filter((f) => f.available);
const iso = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();
let seq = 100;
const uid = () => `00000000-0000-4000-8000-${String(++seq).padStart(12, '0')}`;

function features(overrides: Record<string, boolean> = {}) {
  return MOCK_CATALOG.map((def) => ({
    key: def.key,
    enabled: def.available && (overrides[def.key] ?? def.defaultEnabled),
    updatedAt: iso(300),
    updatedBy: 'admin@example.com',
  }));
}

function repoKinds(f: Record<string, boolean>): RepoKind[] {
  const kinds: RepoKind[] = ['Backend', 'Dashboard'];
  if (f['mobileApp']) kinds.push('Mobile');
  if (f['website']) kinds.push('Website');
  return kinds;
}

function plan(f: Record<string, boolean>): JobStep[] {
  const step = (name: string): JobStep => ({ name, status: 'Pending', message: null, at: null });
  return [
    step('validate'),
    ...repoKinds(f).flatMap((k) => {
      const kind = k.toLowerCase();
      return [step(`repo:${kind}`), step(`config:${kind}`), step(`variables:${kind}`)];
    }),
    step('finalize'),
  ];
}

function stepMessage(name: string, slug: string, dry: boolean): string {
  const [what, kind] = name.split(':');
  const repo = `acme/${slug}-${kind}`;
  switch (what) {
    case 'validate':
      return dry
        ? 'Dry run: nothing will be created on GitHub.'
        : 'GitHub configured for owner acme.';
    case 'repo':
      return dry
        ? `Would create private repository ${repo} from template acme/dr-booking-${kind}.`
        : `Created private repository ${repo} from acme/dr-booking-${kind}.`;
    case 'config':
      return dry
        ? `Would commit clinic.config.json (1 482 characters) to ${repo}: "Brand for clinic".`
        : `Committed clinic.config.json to ${repo}.`;
    case 'variables':
      return dry
        ? `Would set Actions variable CONSOLE_URL on ${repo}. Would set Actions variable CLINIC_ID on ${repo}.`
        : 'Created Actions variable CONSOLE_URL. Created Actions variable CLINIC_ID.';
    default:
      return dry
        ? 'Dry run finished. Nothing was created and the clinic status is unchanged.'
        : 'Provisioned repositories. The clinic is Active.';
  }
}

function finishedJob(clinic: Clinic, dryRun: boolean, minutesAgo: number): Job {
  const f = Object.fromEntries(clinic.features.map((x) => [x.key, x.enabled]));
  const steps = plan(f).map((s, i) => ({
    ...s,
    status: 'Succeeded' as const,
    message: stepMessage(s.name, clinic.slug, dryRun),
    at: iso(minutesAgo - i * 0.02),
  }));
  return {
    id: uid(),
    clinicId: clinic.id,
    dryRun,
    status: 'Succeeded',
    steps,
    startedAt: iso(minutesAgo),
    finishedAt: iso(minutesAgo - 0.3),
    startedBy: 'admin@example.com',
    createdAt: iso(minutesAgo),
  };
}

function seedClinic(
  slug: string,
  nameEn: string,
  nameAr: string,
  primary: string,
  accent: string,
  status: ClinicStatus,
  minutesAgo: number,
  f: Record<string, boolean> = {},
): Clinic {
  const id = uid();
  const compact = slug.replace(/-/g, '');
  return {
    id,
    slug,
    nameAr,
    nameEn,
    taglineAr: 'رعاية أسنان بلمسة إنسانية',
    taglineEn: 'Dental care with a human touch',
    primaryColor: primary,
    accentColor: accent,
    supportPhone: '+201000000001',
    domain: `${compact}.example`,
    androidApplicationId: `com.${compact}.app`,
    iosBundleId: `com.${compact}.app`,
    logoSvg: null,
    status,
    hasApiKey: status === 'Active' || status === 'Suspended',
    apiKeyPrefix: status === 'Active' || status === 'Suspended' ? 'a1b2c3d4' : null,
    createdAt: iso(minutesAgo),
    updatedAt: iso(minutesAgo / 2),
    features: features(f),
    repos: [],
    latestJob: null,
  };
}

function withRepos(c: Clinic): Clinic {
  const f = Object.fromEntries(c.features.map((x) => [x.key, x.enabled]));
  c.repos = repoKinds(f).map((kind) => ({
    kind,
    fullName: `acme/${c.slug}-${kind.toLowerCase()}`,
    htmlUrl: `https://github.com/acme/${c.slug}-${kind.toLowerCase()}`,
  }));
  return c;
}

function seed(): Db {
  const nile = withRepos(
    seedClinic(
      'nile-dental',
      'Nile Dental',
      'نايل دنتال',
      '#3F3CCF',
      '#0E9F9A',
      'Active',
      60 * 24 * 12,
    ),
  );
  const sunrise = seedClinic(
    'sunrise-clinic',
    'Sunrise Clinic',
    'عيادة الشروق',
    '#C2410C',
    '#EAB308',
    'Failed',
    60 * 30,
    { website: false },
  );
  const palm = withRepos(
    seedClinic(
      'palm-orthodontics',
      'Palm Orthodontics',
      'بالم لتقويم الأسنان',
      '#15803D',
      '#2563EB',
      'Suspended',
      60 * 24 * 40,
      { smsNotifications: true },
    ),
  );
  const jobs: Job[] = [
    finishedJob(nile, false, 60 * 24 * 12 - 3),
    finishedJob(nile, true, 60 * 24 * 12 + 20),
  ];
  const failed = finishedJob(sunrise, false, 60 * 29);
  failed.status = 'Failed';
  const at = failed.steps.findIndex((s) => s.name === 'config:dashboard');
  failed.steps[at] = {
    ...failed.steps[at],
    status: 'Failed',
    message: 'GitHub 403: Resource not accessible by personal access token',
  };
  for (let i = at + 1; i < failed.steps.length; i++)
    failed.steps[i] = { ...failed.steps[i], status: 'Pending', message: null, at: null };
  jobs.push(failed);
  nile.latestJob = jobs[0];
  sunrise.latestJob = failed;
  const me: Admin = {
    id: uid(),
    email: 'admin@example.com',
    displayName: 'Platform admin',
    isActive: true,
    createdAt: iso(60 * 24 * 90),
  };
  const audit: AuditEntry[] = [
    {
      id: uid(),
      at: iso(60 * 24 * 12),
      actor: me.email,
      action: 'clinic.created',
      clinicId: nile.id,
      details: { slug: nile.slug },
    },
    {
      id: uid(),
      at: iso(60 * 24 * 12 - 3),
      actor: 'system',
      action: 'provision.succeeded',
      clinicId: nile.id,
      details: {},
    },
    {
      id: uid(),
      at: iso(300),
      actor: me.email,
      action: 'feature.changed',
      clinicId: nile.id,
      details: { key: 'smsNotifications', from: false, to: true },
    },
  ];
  return {
    admins: [
      me,
      {
        id: uid(),
        email: 'sara@example.com',
        displayName: 'Sara Hassan',
        isActive: true,
        createdAt: iso(60 * 24 * 20),
      },
      {
        id: uid(),
        email: 'old.admin@example.com',
        displayName: 'Old admin',
        isActive: false,
        createdAt: iso(60 * 24 * 200),
      },
    ],
    clinics: [nile, sunrise, palm],
    jobs,
    audit,
    me,
  };
}

let db: Db = seed();

/** Restores the seeded fixtures; tests call it so one test's writes never leak into another. */
export function resetMockDb(): void {
  db = seed();
}

function problem(
  status: number,
  code: string,
  errors?: Record<string, string[]>,
  extra: object = {},
) {
  return throwError(
    () => new HttpErrorResponse({ status, error: { code, status, errors, ...extra } }),
  );
}

const ok = <T>(body: T, status = 200): Observable<HttpResponse<T>> =>
  of(new HttpResponse({ status, body: structuredClone(body) })).pipe(delay(120));

function summary(c: Clinic) {
  return {
    id: c.id,
    slug: c.slug,
    nameAr: c.nameAr,
    nameEn: c.nameEn,
    primaryColor: c.primaryColor,
    accentColor: c.accentColor,
    status: c.status,
    enabledFeatureCount: c.features.filter((x) => x.enabled).length,
    repos: c.repos,
    createdAt: c.createdAt,
  };
}

function record(actor: string, action: string, clinicId: string | null, details: object = {}) {
  db.audit.unshift({
    id: uid(),
    at: new Date().toISOString(),
    actor,
    action,
    clinicId,
    details: details as Record<string, unknown>,
  });
}

function validate(d: Partial<ClinicDraft>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  if (!d.slug || !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(d.slug) || d.slug.length > 40)
    errors['slug'] = ['Invalid slug.'];
  if (!d.nameEn?.trim()) errors['nameEn'] = ['Required.'];
  if (!d.nameAr?.trim()) errors['nameAr'] = ['Required.'];
  if (!/^#[0-9a-f]{6}$/i.test(d.primaryColor ?? '')) errors['primaryColor'] = ['Invalid colour.'];
  if (!/^#[0-9a-f]{6}$/i.test(d.accentColor ?? '')) errors['accentColor'] = ['Invalid colour.'];
  return errors;
}

function resolveFeatures(requested: Record<string, boolean> = {}): Record<string, boolean> {
  const out = Object.fromEntries(AVAILABLE.map((x) => [x.key, x.defaultEnabled]));
  for (const [k, v] of Object.entries(requested)) {
    const def = MOCK_CATALOG.find((x) => x.key === k);
    if (def?.available) out[k] = v;
  }
  return out;
}

function configOf(
  c: Pick<
    Clinic,
    | 'slug'
    | 'nameAr'
    | 'nameEn'
    | 'taglineAr'
    | 'taglineEn'
    | 'primaryColor'
    | 'accentColor'
    | 'supportPhone'
    | 'domain'
    | 'androidApplicationId'
    | 'iosBundleId'
  >,
  f: Record<string, boolean>,
  clinicId: string,
) {
  return {
    $schema: './clinic.config.schema.json',
    slug: c.slug,
    name: { ar: c.nameAr, en: c.nameEn },
    tagline: { ar: c.taglineAr, en: c.taglineEn },
    colors: { primary: c.primaryColor.toUpperCase(), accent: c.accentColor.toUpperCase() },
    logo: 'branding/logo.svg',
    supportPhone: c.supportPhone,
    website: { domain: c.domain },
    mobile: { androidApplicationId: c.androidApplicationId, iosBundleId: c.iosBundleId },
    console: { url: 'https://console.example', clinicId },
    features: Object.fromEntries(AVAILABLE.map((x) => [x.key, f[x.key] ?? x.defaultEnabled])),
  };
}

function runJob(clinic: Clinic, job: Job) {
  if (!job.dryRun) clinic.status = 'Provisioning';
  job.status = 'Running';
  job.startedAt = new Date().toISOString();
  let i = 0;
  const tick = () => {
    if (i > 0) {
      const done = job.steps[i - 1];
      done.status = 'Succeeded';
      done.message = stepMessage(done.name, clinic.slug, job.dryRun);
      done.at = new Date().toISOString();
    }
    if (i >= job.steps.length) {
      job.status = 'Succeeded';
      job.finishedAt = new Date().toISOString();
      if (!job.dryRun) {
        clinic.status = 'Active';
        withRepos(clinic);
      }
      clinic.latestJob = { ...job };
      return;
    }
    job.steps[i].status = 'Running';
    job.steps[i].at = new Date().toISOString();
    i++;
    clinic.latestJob = { ...job };
    setTimeout(tick, 700);
  };
  setTimeout(tick, 400);
}

export const mockBackendInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_ROOT)) return next(req);
  const path = req.url.slice(API_ROOT.length).split('?')[0];
  const method = req.method;
  const body = (req.body ?? {}) as Record<string, unknown>;
  const actor = db.me.email;
  const clinicOf = (id: string) => db.clinics.find((c) => c.id === id);
  let m: RegExpMatchArray | null;

  if (method === 'GET' && path === '/platform')
    return ok({ name: 'Moonlight', provisioningMode: 'GitHub' });
  if (method === 'POST' && path === '/auth/login') {
    if (!body['email'] || !body['password']) return problem(400, 'VALIDATION_FAILED');
    if (body['password'] === 'wrong') return problem(401, 'INVALID_CREDENTIALS');
    return ok({ accessToken: 'mock-token', expiresAt: iso(-480), admin: db.me });
  }
  if (method === 'GET' && path === '/me') return ok(db.me);
  if (method === 'GET' && path === '/feature-catalog') return ok(MOCK_CATALOG);

  if (method === 'GET' && path === '/admins') return ok(db.admins);
  if (method === 'POST' && path === '/admins') {
    const email = String(body['email'] ?? '').toLowerCase();
    if (db.admins.some((a) => a.email === email)) return problem(409, 'EMAIL_TAKEN');
    if (!email.includes('@') || String(body['password'] ?? '').length < 10) {
      return problem(400, 'VALIDATION_FAILED', {
        password: ['Password must be at least 10 characters.'],
      });
    }
    const admin: Admin = {
      id: uid(),
      email,
      displayName: String(body['displayName'] || email),
      isActive: true,
      createdAt: new Date().toISOString(),
    };
    db.admins.push(admin);
    record(actor, 'admin.created', null, { email });
    return ok(admin, 201);
  }
  if ((m = path.match(/^\/admins\/([^/]+)\/(deactivate|activate)$/)) && method === 'POST') {
    const admin = db.admins.find((a) => a.id === m![1]);
    if (!admin) return problem(404, 'NOT_FOUND');
    if (
      m[2] === 'deactivate' &&
      (admin.id === db.me.id || db.admins.filter((a) => a.isActive).length <= 1)
    )
      return problem(409, 'LAST_ADMIN');
    admin.isActive = m[2] === 'activate';
    record(actor, `admin.${m[2]}d`, null, { email: admin.email });
    return ok(admin);
  }

  if (method === 'GET' && path === '/audit') {
    const params = new URL(req.urlWithParams, 'http://x').searchParams;
    const clinicId = params.get('clinicId');
    const items = db.audit.filter((a) => !clinicId || a.clinicId === clinicId);
    return ok({ items, total: items.length, page: 1, pageSize: 25 });
  }

  if (method === 'GET' && path === '/clinics') {
    const params = new URL(req.urlWithParams, 'http://x').searchParams;
    const q = (params.get('search') ?? '').toLowerCase();
    const status = params.get('status');
    const items = db.clinics
      .filter(
        (c) =>
          (!status || c.status === status) &&
          (!q || [c.slug, c.nameEn, c.nameAr].some((s) => s.toLowerCase().includes(q))),
      )
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
      .map(summary);
    return ok({ items, total: items.length, page: 1, pageSize: 20 });
  }
  if (method === 'POST' && path === '/clinics/preview') {
    const d = body as unknown as ClinicDraft;
    const errors = validate(d);
    if (Object.keys(errors).length) return problem(400, 'VALIDATION_FAILED', errors);
    if (db.clinics.some((c) => c.slug === d.slug)) return problem(409, 'SLUG_TAKEN');
    const f = resolveFeatures(d.features);
    return ok({
      config: configOf(d, f, '(assigned when the clinic is created)'),
      repos: repoKinds(f).map((k) => `${d.slug}-${k.toLowerCase()}`),
    });
  }
  if (method === 'POST' && path === '/clinics') {
    const d = body as unknown as ClinicDraft;
    const errors = validate(d);
    if (Object.keys(errors).length) return problem(400, 'VALIDATION_FAILED', errors);
    if (db.clinics.some((c) => c.slug === d.slug)) return problem(409, 'SLUG_TAKEN');
    for (const [k, v] of Object.entries(d.features ?? {})) {
      const def = MOCK_CATALOG.find((x) => x.key === k);
      if (def && !def.available && v)
        return problem(409, 'FEATURE_NOT_AVAILABLE', undefined, { feature: k });
    }
    const clinic = seedClinic(
      d.slug,
      d.nameEn,
      d.nameAr,
      d.primaryColor.toUpperCase(),
      d.accentColor.toUpperCase(),
      'Draft',
      0,
      resolveFeatures(d.features),
    );
    Object.assign(clinic, {
      taglineAr: d.taglineAr,
      taglineEn: d.taglineEn,
      supportPhone: d.supportPhone,
      domain: d.domain,
      androidApplicationId: d.androidApplicationId || `com.${d.slug.replace(/-/g, '')}.app`,
      iosBundleId: d.iosBundleId || `com.${d.slug.replace(/-/g, '')}.app`,
      logoSvg: d.logoSvg || null,
      hasApiKey: false,
      apiKeyPrefix: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    });
    db.clinics.unshift(clinic);
    record(actor, 'clinic.created', clinic.id, { slug: clinic.slug });
    return ok(clinic, 201);
  }

  if ((m = path.match(/^\/clinics\/([^/]+)(?:\/(.+))?$/))) {
    const clinic = clinicOf(m[1]);
    if (!clinic) return problem(404, 'CLINIC_NOT_FOUND');
    const sub = m[2] ?? '';
    const f = () => Object.fromEntries(clinic.features.map((x) => [x.key, x.enabled]));

    if (method === 'GET' && sub === '') return ok(clinic);
    if (method === 'PATCH' && sub === '') {
      const patch = body as Record<string, string>;
      if (patch['primaryColor'] !== undefined && !/^#[0-9a-f]{6}$/i.test(patch['primaryColor']))
        return problem(400, 'VALIDATION_FAILED', { primaryColor: ['Invalid colour.'] });
      for (const [k, v] of Object.entries(patch)) {
        if (k === 'logoSvg') clinic.logoSvg = v || null;
        else if (v !== null && v !== undefined)
          (clinic as unknown as Record<string, unknown>)[k] = v;
      }
      clinic.updatedAt = new Date().toISOString();
      record(actor, 'clinic.updated', clinic.id, { fields: Object.keys(patch) });
      return ok(clinic);
    }
    if (method === 'POST' && (sub === 'suspend' || sub === 'activate')) {
      clinic.status = sub === 'suspend' ? 'Suspended' : clinic.repos.length ? 'Active' : 'Draft';
      record(actor, `clinic.${sub === 'suspend' ? 'suspended' : 'activated'}`, clinic.id);
      return ok(clinic);
    }
    if (method === 'PUT' && sub.startsWith('features/')) {
      const key = sub.split('/')[1];
      const def = MOCK_CATALOG.find((x) => x.key === key);
      if (!def) return problem(404, 'FEATURE_NOT_FOUND');
      if (!def.available && body['enabled'])
        return problem(409, 'FEATURE_NOT_AVAILABLE', undefined, { feature: key });
      const row = clinic.features.find((x) => x.key === key)!;
      record(actor, 'feature.changed', clinic.id, {
        key,
        from: row.enabled,
        to: !!body['enabled'],
      });
      row.enabled = !!body['enabled'];
      row.updatedAt = new Date().toISOString();
      row.updatedBy = actor;
      return ok(row);
    }
    if (method === 'POST' && (sub === 'provision' || sub === 'sync-config')) {
      if (
        db.jobs.some(
          (j) => j.clinicId === clinic.id && (j.status === 'Queued' || j.status === 'Running'),
        )
      )
        return problem(409, 'JOB_IN_PROGRESS');
      if (clinic.status === 'Suspended' || (sub === 'sync-config' && clinic.status !== 'Active'))
        return problem(409, 'CLINIC_NOT_ACTIVE');
      const dryRun = sub === 'provision' ? body['dryRun'] !== false : false;
      let apiKey: string | null = null;
      if (!dryRun && !clinic.hasApiKey) {
        apiKey = 'ck_9f8e7d6c_0123456789abcdef0123456789abcdef0123456789abcdef';
        clinic.hasApiKey = true;
        clinic.apiKeyPrefix = '9f8e7d6c';
      }
      const job: Job = {
        id: uid(),
        clinicId: clinic.id,
        dryRun,
        status: 'Queued',
        steps: plan(f()),
        startedAt: null,
        finishedAt: null,
        startedBy: actor,
        createdAt: new Date().toISOString(),
      };
      db.jobs.unshift(job);
      clinic.latestJob = job;
      record(actor, 'provision.requested', clinic.id, { jobId: job.id, dryRun });
      runJob(clinic, job);
      return ok({ job: { ...job }, apiKey }, 202);
    }
    if (method === 'GET' && sub === 'jobs')
      return ok(
        db.jobs
          .filter((j) => j.clinicId === clinic.id)
          .map((j) => ({ ...j, steps: j.steps.map((s) => ({ ...s })) })),
      );
    if (method === 'POST' && sub === 'api-key/rotate') {
      clinic.hasApiKey = true;
      clinic.apiKeyPrefix = '5e4d3c2b';
      record(actor, 'api-key.rotated', clinic.id, { prefix: clinic.apiKeyPrefix });
      return ok({
        apiKey: 'ck_5e4d3c2b_fedcba9876543210fedcba9876543210fedcba98765432',
        prefix: '5e4d3c2b',
      });
    }
    if (method === 'GET' && sub === 'config-preview') {
      return of(
        new HttpResponse({
          status: 200,
          body: JSON.stringify(configOf(clinic, f(), clinic.id), null, 2) + '\n',
        }),
      ).pipe(delay(120));
    }
  }

  if (method === 'GET' && (m = path.match(/^\/jobs\/([^/]+)$/))) {
    const job = db.jobs.find((j) => j.id === m![1]);
    return job
      ? ok({ ...job, steps: job.steps.map((s) => ({ ...s })) })
      : problem(404, 'JOB_NOT_FOUND');
  }

  return problem(404, 'NOT_FOUND');
};
