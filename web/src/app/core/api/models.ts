export type AppLang = 'ar' | 'en';

export type ClinicStatus = 'Draft' | 'Provisioning' | 'Active' | 'Failed' | 'Suspended';
export type RepoKind = 'Backend' | 'Dashboard' | 'Mobile' | 'Website';
export type JobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed';
export type StepStatus = 'Pending' | 'Running' | 'Succeeded' | 'Skipped' | 'Failed';

export interface Platform {
  name: string;
  provisioningMode: 'GitHub' | 'DryRun';
}

export interface Admin {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  createdAt: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  admin: Admin;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ProblemDetails {
  code?: string;
  title?: string;
  detail?: string;
  feature?: string;
  errors?: Record<string, string[]>;
}

export interface FeatureDefinition {
  key: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string;
  descriptionEn: string;
  group: string;
  available: boolean;
  defaultEnabled: boolean;
  createsRepo: RepoKind | null;
}

export interface FeatureState {
  key: string;
  enabled: boolean;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface Repo {
  kind: RepoKind;
  fullName: string;
  htmlUrl: string;
}

export interface ClinicSummary {
  id: string;
  slug: string;
  nameAr: string;
  nameEn: string;
  primaryColor: string;
  accentColor: string;
  status: ClinicStatus;
  enabledFeatureCount: number;
  repos: Repo[];
  createdAt: string;
}

export interface JobStep {
  name: string;
  status: StepStatus;
  message: string | null;
  at: string | null;
}

export interface Job {
  id: string;
  clinicId: string;
  dryRun: boolean;
  status: JobStatus;
  steps: JobStep[];
  startedAt: string | null;
  finishedAt: string | null;
  startedBy: string;
  createdAt: string;
}

export interface Clinic {
  id: string;
  slug: string;
  nameAr: string;
  nameEn: string;
  taglineAr: string;
  taglineEn: string;
  primaryColor: string;
  accentColor: string;
  supportPhone: string;
  domain: string;
  androidApplicationId: string;
  iosBundleId: string;
  logoSvg: string | null;
  status: ClinicStatus;
  hasApiKey: boolean;
  apiKeyPrefix: string | null;
  createdAt: string;
  updatedAt: string;
  features: FeatureState[];
  repos: Repo[];
  latestJob: Job | null;
}

/** Fields of a new clinic as the wizard collects them. */
export interface ClinicDraft {
  slug: string;
  nameAr: string;
  nameEn: string;
  taglineAr: string;
  taglineEn: string;
  primaryColor: string;
  accentColor: string;
  supportPhone: string;
  domain: string;
  androidApplicationId: string;
  iosBundleId: string;
  logoSvg: string | null;
  features: Record<string, boolean>;
}

export type ClinicPatch = Partial<Omit<ClinicDraft, 'slug' | 'features'>>;

export interface ConfigPreview {
  config: Record<string, unknown>;
  repos: string[];
}

export interface ProvisionResponse {
  job: Job;
  apiKey: string | null;
}

export interface AuditEntry {
  id: string;
  at: string;
  actor: string;
  action: string;
  clinicId: string | null;
  details: Record<string, unknown>;
}
