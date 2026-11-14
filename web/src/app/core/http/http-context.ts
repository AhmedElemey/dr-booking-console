import { HttpContextToken } from '@angular/common/http';

/** Set on requests whose failure the caller handles itself (no toast). */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);
