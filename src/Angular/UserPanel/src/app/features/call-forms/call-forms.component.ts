import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CallJobType } from '../../core/models/call-job';
import { CallJobsApi } from '../../core/services/call-jobs.api';

type FormTab = CallJobType;

@Component({
  selector: 'app-call-forms',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './call-forms.component.html',
  styleUrl: './call-forms.component.scss',
})
export class CallFormsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(CallJobsApi);
  private readonly router = inject(Router);

  readonly tabs: FormTab[] = ['ExtToExt', 'MobileToExt', 'MobileToMobile'];
  activeTab = signal<FormTab>('ExtToExt');
  submitting = signal(false);
  successMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);

  readonly extToExtForm = this.fb.nonNullable.group({
    from: ['', Validators.required],
    to: ['', Validators.required],
    timeoutSec: [60, [Validators.required, Validators.min(5)]],
  });

  readonly mobileToExtForm = this.fb.nonNullable.group({
    mobile1: ['', Validators.required],
    to: ['', Validators.required],
    timeoutSec: [60, [Validators.required, Validators.min(5)]],
  });

  readonly mobileToMobileForm = this.fb.nonNullable.group({
    mobile1: ['', Validators.required],
    mobile2: ['', Validators.required],
    timeoutSec: [60, [Validators.required, Validators.min(5)]],
  });

  ngOnInit(): void {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  selectTab(tab: FormTab): void {
    this.activeTab.set(tab);
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  submit(): void {
    const tab = this.activeTab();
    this.successMessage.set(null);
    this.errorMessage.set(null);

    if (tab === 'ExtToExt') {
      if (this.extToExtForm.invalid) {
        this.extToExtForm.markAllAsTouched();
        return;
      }
      const v = this.extToExtForm.getRawValue();
      this.runAdd({
        type: 'ExtToExt',
        from: v.from.trim(),
        to: v.to.trim(),
        timeoutSec: Number(v.timeoutSec),
      });
      return;
    }

    if (tab === 'MobileToExt') {
      if (this.mobileToExtForm.invalid) {
        this.mobileToExtForm.markAllAsTouched();
        return;
      }
      const v = this.mobileToExtForm.getRawValue();
      this.runAdd({
        type: 'MobileToExt',
        mobile1: v.mobile1.trim(),
        from: v.mobile1.trim(),
        to: v.to.trim(),
        timeoutSec: Number(v.timeoutSec),
      });
      return;
    }

    if (this.mobileToMobileForm.invalid) {
      this.mobileToMobileForm.markAllAsTouched();
      return;
    }
    const v = this.mobileToMobileForm.getRawValue();
    this.runAdd({
      type: 'MobileToMobile',
      mobile1: v.mobile1.trim(),
      mobile2: v.mobile2.trim(),
      from: v.mobile1.trim(),
      to: v.mobile2.trim(),
      timeoutSec: Number(v.timeoutSec),
    });
  }

  private runAdd(body: {
    type: CallJobType;
    from?: string;
    to?: string;
    mobile1?: string;
    mobile2?: string;
    timeoutSec?: number;
  }): void {
    this.submitting.set(true);
    this.api.add(body).subscribe({
      next: (job) => {
        this.submitting.set(false);
        this.successMessage.set(job.id);
        void this.router.navigate(['/jobs']);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.errorMessage.set(err instanceof Error ? err.message : String(err));
      },
    });
  }

  tabLabelKey(tab: FormTab): string {
    switch (tab) {
      case 'ExtToExt':
        return 'FORM.EXT_TO_EXT';
      case 'MobileToExt':
        return 'FORM.MOBILE_TO_EXT';
      case 'MobileToMobile':
        return 'FORM.MOBILE_TO_MOBILE';
      default: {
        const _exhaustive: never = tab;
        return _exhaustive;
      }
    }
  }
}
