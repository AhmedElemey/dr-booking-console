import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .load()
    .pipe(
      map((admin) =>
        admin ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } }),
      ),
    );
};

/** For the login page: a signed-in admin goes straight to the clinics list. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.load().pipe(map((admin) => (admin ? router.createUrlTree(['/clinics']) : true)));
};
