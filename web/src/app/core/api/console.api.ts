import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl } from '../config/api-url';
import {
  Admin,
  AuditEntry,
  Clinic,
  ClinicDraft,
  ClinicPatch,
  ClinicStatus,
  ClinicSummary,
  ConfigPreview,
  FeatureDefinition,
  FeatureState,
  Job,
  LoginResponse,
  Page,
  Platform,
  ProvisionResponse,
} from './models';

@Injectable({ providedIn: 'root' })
export class ConsoleApi {
  private readonly http = inject(HttpClient);

  platform(): Observable<Platform> {
    return this.http.get<Platform>(apiUrl('/platform'));
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(apiUrl('/auth/login'), { email, password });
  }

  me(): Observable<Admin> {
    return this.http.get<Admin>(apiUrl('/me'));
  }

  catalog(): Observable<FeatureDefinition[]> {
    return this.http.get<FeatureDefinition[]>(apiUrl('/feature-catalog'));
  }

  clinics(search: string, status: ClinicStatus | '', page: number, pageSize = 20) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<Page<ClinicSummary>>(apiUrl('/clinics'), { params });
  }

  clinic(id: string): Observable<Clinic> {
    return this.http.get<Clinic>(apiUrl(`/clinics/${id}`));
  }

  previewClinic(draft: ClinicDraft): Observable<ConfigPreview> {
    return this.http.post<ConfigPreview>(apiUrl('/clinics/preview'), draft);
  }

  createClinic(draft: ClinicDraft): Observable<Clinic> {
    return this.http.post<Clinic>(apiUrl('/clinics'), draft);
  }

  updateClinic(id: string, patch: ClinicPatch): Observable<Clinic> {
    return this.http.patch<Clinic>(apiUrl(`/clinics/${id}`), patch);
  }

  suspend(id: string): Observable<Clinic> {
    return this.http.post<Clinic>(apiUrl(`/clinics/${id}/suspend`), {});
  }

  activate(id: string): Observable<Clinic> {
    return this.http.post<Clinic>(apiUrl(`/clinics/${id}/activate`), {});
  }

  setFeature(id: string, key: string, enabled: boolean): Observable<FeatureState> {
    return this.http.put<FeatureState>(apiUrl(`/clinics/${id}/features/${key}`), { enabled });
  }

  provision(id: string, dryRun: boolean): Observable<ProvisionResponse> {
    return this.http.post<ProvisionResponse>(apiUrl(`/clinics/${id}/provision`), { dryRun });
  }

  syncConfig(id: string): Observable<ProvisionResponse> {
    return this.http.post<ProvisionResponse>(apiUrl(`/clinics/${id}/sync-config`), {});
  }

  jobs(id: string): Observable<Job[]> {
    return this.http.get<Job[]>(apiUrl(`/clinics/${id}/jobs`));
  }

  rotateKey(id: string): Observable<{ apiKey: string; prefix: string }> {
    return this.http.post<{ apiKey: string; prefix: string }>(
      apiUrl(`/clinics/${id}/api-key/rotate`),
      {},
    );
  }

  configPreview(id: string): Observable<string> {
    return this.http.get(apiUrl(`/clinics/${id}/config-preview`), { responseType: 'text' });
  }

  audit(clinicId: string | null, page: number, pageSize = 25): Observable<Page<AuditEntry>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (clinicId) params = params.set('clinicId', clinicId);
    return this.http.get<Page<AuditEntry>>(apiUrl('/audit'), { params });
  }

  admins(): Observable<Admin[]> {
    return this.http.get<Admin[]>(apiUrl('/admins'));
  }

  createAdmin(email: string, password: string, displayName: string): Observable<Admin> {
    return this.http.post<Admin>(apiUrl('/admins'), { email, password, displayName });
  }

  deactivateAdmin(id: string): Observable<Admin> {
    return this.http.post<Admin>(apiUrl(`/admins/${id}/deactivate`), {});
  }

  activateAdmin(id: string): Observable<Admin> {
    return this.http.post<Admin>(apiUrl(`/admins/${id}/activate`), {});
  }
}
