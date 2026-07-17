import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';
import { ConnectionStatus, SiteSettings } from '../../core/models/asterisk.models';
import { AsteriskApiService } from '../../core/services/asterisk-api.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [TranslatePipe, ReactiveFormsModule],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(AsteriskApiService);
  private readonly fb = inject(FormBuilder);

  readonly apiBaseUrl = environment.apiBaseUrl;
  readonly hubUrl = `${environment.apiBaseUrl.replace(/\/$/, '')}${environment.hubPath}`;

  readonly settings = signal<SiteSettings | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly testResult = signal<ConnectionStatus | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly testing = signal(false);

  readonly form = this.fb.nonNullable.group({
    host: ['', Validators.required],
    port: [5038, [Validators.required, Validators.min(1), Validators.max(65535)]],
    username: ['', Validators.required],
    secret: [''],
    clearSecret: [false],
    channelTech: ['PJSIP', Validators.required],
    originateVia: ['LocalContext', Validators.required],
    originateContext: ['from-internal', Validators.required],
    musicOnHoldClass: ['default', Validators.required],
    defaultTrunk: [''],
    trunkPeerFilter: [''],
    defaultTimeoutMs: [30000, [Validators.required, Validators.min(1000)]],
    defaultCallerId: [''],
    keepAlive: [true],
    pingIntervalMs: [10000, [Validators.required, Validators.min(1000)]],
    autoConnectOnStartup: [true],
    recordingEnabled: [true],
    recordingLocalDirectory: [''],
    recordingHttpBaseUrl: [''],
    recordingAsteriskDirectory: ['/var/spool/asterisk/monitor'],
    recordingFormat: ['wav'],
    reconnectAfterSave: [true],
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);
    this.api.getSiteSettings().subscribe({
      next: (s) => {
        this.settings.set(s);
        if (s) {
          this.form.patchValue({
            host: s.host ?? '',
            port: s.port && s.port > 0 ? s.port : 5038,
            username: s.username ?? '',
            secret: '',
            clearSecret: false,
            channelTech: s.channelTech || 'PJSIP',
            originateVia: s.originateVia || 'LocalContext',
            originateContext: s.originateContext || 'from-internal',
            musicOnHoldClass: s.musicOnHoldClass || 'default',
            defaultTrunk: s.defaultTrunk ?? '',
            trunkPeerFilter: s.trunkPeerFilter ?? '',
            defaultTimeoutMs: s.defaultTimeoutMs || 30000,
            defaultCallerId: s.defaultCallerId ?? '',
            keepAlive: s.keepAlive,
            pingIntervalMs: s.pingIntervalMs || 10000,
            autoConnectOnStartup: s.autoConnectOnStartup,
            recordingEnabled: s.recordingEnabled ?? true,
            recordingLocalDirectory: s.recordingLocalDirectory ?? '',
            recordingHttpBaseUrl: s.recordingHttpBaseUrl ?? '',
            recordingAsteriskDirectory: s.recordingAsteriskDirectory ?? '/var/spool/asterisk/monitor',
            recordingFormat: s.recordingFormat || 'wav',
            reconnectAfterSave: true,
          });
        }
        this.loading.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);
    this.api
      .updateSiteSettings({
        host: v.host.trim(),
        port: Number(v.port),
        username: v.username.trim(),
        secret: v.clearSecret ? null : v.secret.trim() || null,
        clearSecret: v.clearSecret,
        channelTech: v.channelTech.trim(),
        originateVia: v.originateVia.trim(),
        originateContext: v.originateContext.trim(),
        musicOnHoldClass: v.musicOnHoldClass.trim() || 'default',
        defaultTrunk: v.defaultTrunk.trim() || null,
        trunkPeerFilter: v.trunkPeerFilter.trim() || null,
        defaultTimeoutMs: Number(v.defaultTimeoutMs),
        defaultCallerId: v.defaultCallerId.trim() || null,
        keepAlive: v.keepAlive,
        pingIntervalMs: Number(v.pingIntervalMs),
        autoConnectOnStartup: v.autoConnectOnStartup,
        recordingEnabled: v.recordingEnabled,
        recordingLocalDirectory: v.recordingLocalDirectory.trim() || null,
        recordingHttpBaseUrl: v.recordingHttpBaseUrl.trim() || null,
        recordingAsteriskDirectory: v.recordingAsteriskDirectory.trim() || null,
        recordingFormat: v.recordingFormat.trim() || 'wav',
        reconnectAfterSave: v.reconnectAfterSave,
      })
      .subscribe({
        next: (r) => {
          this.saving.set(false);
          if (!r.isSuccess) {
            this.error.set(r.errorMessage || 'Save failed');
            return;
          }
          const saved = r.data?.[0] ?? null;
          this.settings.set(saved);
          this.success.set('SETTINGS.SAVE_OK');
          this.form.patchValue({ secret: '', clearSecret: false });
        },
        error: (err: Error) => {
          this.saving.set(false);
          this.error.set(err.message);
        },
      });
  }

  testConnection(): void {
    this.testing.set(true);
    this.error.set(null);
    this.success.set(null);
    this.testResult.set(null);
    this.api.testConnection().subscribe({
      next: (r) => {
        this.testing.set(false);
        if (!r.isSuccess) {
          this.error.set(r.errorMessage || 'SETTINGS.TEST_FAIL');
          return;
        }
        const status = r.data?.[0] ?? null;
        this.testResult.set(status);
        this.success.set('SETTINGS.TEST_OK');
      },
      error: (err: Error) => {
        this.testing.set(false);
        this.error.set(err.message);
      },
    });
  }
}
