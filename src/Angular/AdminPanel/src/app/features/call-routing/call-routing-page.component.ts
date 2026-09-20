import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CallRoute, CallRouteLookupResult, CallRouteUpsertRequest } from '../../core/models/call-routing.models';
import { CallRoutingApiService } from '../../core/services/call-routing-api.service';

@Component({
  selector: 'app-call-routing-page',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './call-routing-page.component.html',
  styleUrl: './call-routing-page.component.scss',
})
export class CallRoutingPageComponent implements OnInit {
  private readonly api = inject(CallRoutingApiService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly rows = signal<CallRoute[]>([]);
  readonly totalCount = signal(0);
  readonly modalOpen = signal(false);
  readonly isEdit = signal(false);

  quickSearch = '';
  statusFilter: 'all' | 'active' | 'inactive' = 'all';
  draggedIndex: number | null = null;

  form: CallRouteUpsertRequest = {
    id: null,
    callerNumber: '',
    contactName: '',
    description: '',
    targetExtension: '',
    targetExternalNumber: '',
    extensionTimeout: 15,
    externalTimeout: 30,
    outboundTrunk: '',
    isActive: true,
    priority: 1,
  };

  lookupTester = {
    callerNumber: '',
    busy: false,
    result: null as CallRouteLookupResult | null,
    error: null as string | null,
  };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);

    const activeParam =
      this.statusFilter === 'active' ? true : this.statusFilter === 'inactive' ? false : undefined;

    this.api.getList(this.quickSearch, activeParam).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.isSuccess) {
          this.rows.set(res.data || []);
          this.totalCount.set(res.data?.length || 0);
        } else {
          this.error.set(res.errorMessage || 'Failed to load call routes');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.message || 'Network error loading call routes');
      },
    });
  }

  openAdd(): void {
    // Next priority is max priority + 1
    const maxPrio = this.rows().reduce((max, r) => Math.max(max, r.priority || 0), 0);
    this.form = {
      id: null,
      callerNumber: '',
      contactName: '',
      description: '',
      targetExtension: '',
      targetExternalNumber: '',
      extensionTimeout: 15,
      externalTimeout: 30,
      outboundTrunk: '',
      isActive: true,
      priority: maxPrio + 1,
    };
    this.isEdit.set(false);
    this.error.set(null);
    this.success.set(null);
    this.modalOpen.set(true);
  }

  openEdit(row: CallRoute): void {
    this.form = {
      id: row.id,
      callerNumber: row.callerNumber,
      contactName: row.contactName || '',
      description: row.description || '',
      targetExtension: row.targetExtension || '',
      targetExternalNumber: row.targetExternalNumber || '',
      extensionTimeout: row.extensionTimeout || 15,
      externalTimeout: row.externalTimeout || 30,
      outboundTrunk: row.outboundTrunk || '',
      isActive: row.isActive,
      priority: row.priority || 1,
    };
    this.isEdit.set(true);
    this.error.set(null);
    this.success.set(null);
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
  }

  save(): void {
    if (!this.form.callerNumber?.trim()) {
      this.error.set(this.i18n.instant('CALL_ROUTING.ERR_CALLER_REQUIRED') || 'Caller number is required');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);

    this.api.save(this.form).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.isSuccess) {
          this.modalOpen.set(false);
          this.success.set(
            this.isEdit()
              ? this.i18n.instant('CALL_ROUTING.MSG_UPDATED') || 'Call route updated successfully'
              : this.i18n.instant('CALL_ROUTING.MSG_CREATED') || 'Call route created successfully',
          );
          this.reload();
        } else {
          this.error.set(res.errorMessage || 'Failed to save call route');
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err.message || 'Error saving call route');
      },
    });
  }

  toggleActive(row: CallRoute): void {
    this.busy.set(true);
    this.api.toggleActive(row.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.isSuccess && res.data?.length) {
          const updated = res.data[0];
          this.rows.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
        } else {
          this.error.set(res.errorMessage || 'Failed to toggle status');
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err.message || 'Error toggling status');
      },
    });
  }

  deleteRow(row: CallRoute): void {
    const confirmMsg =
      this.i18n.instant('CALL_ROUTING.CONFIRM_DELETE', { caller: row.callerNumber }) ||
      `Are you sure you want to delete route for ${row.callerNumber}?`;

    if (!confirm(confirmMsg)) return;

    this.busy.set(true);
    this.api.delete(row.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.isSuccess) {
          this.success.set(this.i18n.instant('CALL_ROUTING.MSG_DELETED') || 'Route deleted successfully');
          this.reload();
        } else {
          this.error.set(res.errorMessage || 'Failed to delete route');
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err.message || 'Error deleting route');
      },
    });
  }

  // --- Drag and Drop Reordering Handlers ---
  onDragStart(index: number, event: DragEvent): void {
    this.draggedIndex = index;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', index.toString());
    }
  }

  onDragOver(index: number, event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  onDrop(targetIndex: number, event: DragEvent): void {
    event.preventDefault();
    if (this.draggedIndex === null || this.draggedIndex === targetIndex) return;

    const list = [...this.rows()];
    const [moved] = list.splice(this.draggedIndex, 1);
    list.splice(targetIndex, 0, moved);

    this.draggedIndex = null;
    this.recalcAndSavePriorities(list);
  }

  moveUp(index: number): void {
    if (index <= 0) return;
    const list = [...this.rows()];
    const temp = list[index - 1];
    list[index - 1] = list[index];
    list[index] = temp;
    this.recalcAndSavePriorities(list);
  }

  moveDown(index: number): void {
    if (index >= this.rows().length - 1) return;
    const list = [...this.rows()];
    const temp = list[index + 1];
    list[index + 1] = list[index];
    list[index] = temp;
    this.recalcAndSavePriorities(list);
  }

  private recalcAndSavePriorities(list: CallRoute[]): void {
    // Highest row gets highest priority: length, length - 1, ...
    const n = list.length;
    const orderItems: { id: string; priority: number }[] = [];

    const updated = list.map((item, idx) => {
      const prio = (n - idx) * 10;
      orderItems.push({ id: item.id, priority: prio });
      return { ...item, priority: prio };
    });

    this.rows.set(updated);
    this.busy.set(true);

    this.api.reorder(orderItems).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.isSuccess) {
          this.success.set(this.i18n.instant('CALL_ROUTING.MSG_REORDERED') || 'Priority order saved successfully');
          setTimeout(() => this.success.set(null), 2500);
        } else {
          this.error.set(res.errorMessage || 'Failed to save reordered priorities');
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err.message || 'Error saving priority reorder');
      },
    });
  }

  runTestLookup(): void {
    const num = this.lookupTester.callerNumber?.trim();
    if (!num) return;

    this.lookupTester.busy = true;
    this.lookupTester.error = null;
    this.lookupTester.result = null;

    this.api.lookup({ callerNumber: num, source: 'simulator' }).subscribe({
      next: (res) => {
        this.lookupTester.busy = false;
        if (res.isSuccess && res.data?.length) {
          this.lookupTester.result = res.data[0];
        } else {
          this.lookupTester.error = res.errorMessage || 'Lookup failed';
        }
      },
      error: (err) => {
        this.lookupTester.busy = false;
        this.lookupTester.error = err.message || 'Network error during lookup';
      },
    });
  }
}
