import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { resolveLandingRoute } from '../../core/role-access';
import { ThemeService } from '../../core/theme.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly themeService = inject(ThemeService);

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly showPassword = signal(false);

  protected readonly loginForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set('');

    this.authService
      .login(this.loginForm.getRawValue())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => void this.router.navigateByUrl(resolveLandingRoute(response.user.role)),
        error: () => this.errorMessage.set('Unable to sign in with the provided credentials.'),
      });
  }

  protected toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  protected togglePasswordVisibility(): void {
    this.showPassword.update((current) => !current);
  }
}
