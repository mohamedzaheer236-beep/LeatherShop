import { Component, EventEmitter, Output, ChangeDetectionStrategy, ChangeDetectorRef, inject, OnInit } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { CalendarModule } from 'primeng/calendar';
import { BroadcastHistory, BroadcastRecipient, BroadcastDeliverySummary } from '../../models/broadcast.model';
import { BroadcastService } from '../../services/broadcast.service';

@Component({
  selector: 'app-broadcast-history',
  standalone: true,
  imports: [DatePipe, NgClass, TableModule, TagModule, PaginatorModule, DialogModule, ButtonModule, DropdownModule, TooltipModule, OverlayPanelModule, FormsModule, InputTextModule, CalendarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './broadcast-history.component.html',
  styleUrl: './broadcast-history.component.scss',
})
export class BroadcastHistoryComponent implements OnInit {
  private broadcastService = inject(BroadcastService);
  private cdr = inject(ChangeDetectorRef);

  // History table state — self-managed
  history: BroadcastHistory[] = [];
  totalRecords = 0;
  pageSize = 10;
  loading = false;
  sortField = 'sentAt';
  sortOrder = -1; // -1 desc, 1 asc

  // Column filter state
  showFilters = false;
  filters: {
    templateSearch: string;
    recipientsFilter: string;
    sentFilter: string;
    deliveredFilter: string;
    readFilter: string;
    failedFilter: string;
    dateSearch: Date | null;
  } = {
    templateSearch: '',
    recipientsFilter: '',
    sentFilter: '',
    deliveredFilter: '',
    readFilter: '',
    failedFilter: '',
    dateSearch: null,
  };
  hasActiveFilters = false;

  // Expose to parent for refresh after send
  @Output() loaded = new EventEmitter<void>();

  // Recipients dialog state
  showRecipientsDialog = false;
  selectedBroadcast: BroadcastHistory | null = null;
  recipients: BroadcastRecipient[] = [];
  recipientsTotalRecords = 0;
  recipientsPage = 1;
  recipientsPageSize = 20;
  recipientsLoading = false;
  summary: BroadcastDeliverySummary | null = null;
  statusFilter = '';
  retrying = false;
  selectedRetryRecipient: BroadcastRecipient | null = null;

  statusFilterOptions = [
    { label: 'All Statuses', value: '' },
    { label: 'Queued', value: 'Queued' },
    { label: 'Sent', value: 'Sent' },
    { label: 'Delivered', value: 'Delivered' },
    { label: 'Read', value: 'Read' },
    { label: 'Failed', value: 'Failed' },
  ];

  ngOnInit(): void {
    this.loadHistory(1);
  }

  applyFilters(): void {
    this.updateHasActiveFilters();
    this.loadHistory(1);
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
    if (!this.showFilters && this.hasActiveFilters) {
      this.resetAll();
    }
    this.cdr.markForCheck();
  }

  resetAll(): void {
    this.sortField = 'sentAt';
    this.sortOrder = -1;
    this.filters = {
      templateSearch: '',
      recipientsFilter: '',
      sentFilter: '',
      deliveredFilter: '',
      readFilter: '',
      failedFilter: '',
      dateSearch: null,
    };
    this.hasActiveFilters = false;
    this.loadHistory(1);
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / (event.rows ?? this.pageSize)) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    if (event.sortField) {
      this.sortField = event.sortField as string;
      this.sortOrder = event.sortOrder ?? -1;
    }
    this.loadHistory(page);
  }

  /** Public so parent can call refresh after broadcast send */
  loadHistory(page = 1): void {
    this.loading = true;
    this.cdr.markForCheck();
    const sortOrderStr = this.sortOrder === 1 ? 'asc' : 'desc';
    this.broadcastService.getBroadcastHistory(
      page, this.pageSize, this.sortField, sortOrderStr, this.getActiveFilters()
    ).subscribe({
      next: result => {
        this.history = result.items;
        this.totalRecords = result.totalCount;
        this.loading = false;
        this.loaded.emit();
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private getActiveFilters(): Record<string, string> | undefined {
    const active: Record<string, string> = {};
    const f = this.filters;
    const s = (v: unknown) => String(v ?? '').trim();
    if (s(f.templateSearch)) active['templateSearch'] = s(f.templateSearch);
    if (s(f.recipientsFilter)) active['recipientsFilter'] = s(f.recipientsFilter);
    if (s(f.sentFilter)) active['sentFilter'] = s(f.sentFilter);
    if (s(f.deliveredFilter)) active['deliveredFilter'] = s(f.deliveredFilter);
    if (s(f.readFilter)) active['readFilter'] = s(f.readFilter);
    if (s(f.failedFilter)) active['failedFilter'] = s(f.failedFilter);
    if (f.dateSearch) {
      const d = f.dateSearch;
      active['dateSearch'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return Object.keys(active).length > 0 ? active : undefined;
  }

  private updateHasActiveFilters(): void {
    const f = this.filters;
    const s = (v: unknown) => String(v ?? '').trim();
    this.hasActiveFilters = !!(s(f.templateSearch) || s(f.recipientsFilter) || s(f.sentFilter) || s(f.deliveredFilter) || s(f.readFilter) || s(f.failedFilter) || f.dateSearch);
  }

  openRecipients(broadcast: BroadcastHistory): void {
    this.selectedBroadcast = broadcast;
    this.showRecipientsDialog = true;
    this.statusFilter = '';
    this.recipientsPage = 1;
    this.loadSummary(broadcast.id);
    this.loadRecipients(broadcast.id);
  }

  onRecipientsPageChange(event: PaginatorState): void {
    this.recipientsPage = (event.page ?? 0) + 1;
    this.recipientsPageSize = event.rows ?? this.recipientsPageSize;
    if (this.selectedBroadcast) {
      this.loadRecipients(this.selectedBroadcast.id);
    }
  }

  onStatusFilterChange(): void {
    this.recipientsPage = 1;
    if (this.selectedBroadcast) {
      this.loadRecipients(this.selectedBroadcast.id);
    }
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warning' | 'danger' | 'secondary' {
    switch (status) {
      case 'Delivered': return 'success';
      case 'Read': return 'info';
      case 'Sent': return 'warning';
      case 'Failed': return 'danger';
      default: return 'secondary';
    }
  }

  retryFailed(): void {
    if (!this.selectedBroadcast || this.retrying) return;
    this.retrying = true;
    this.broadcastService.retryFailedRecipients(this.selectedBroadcast.id).subscribe({
      next: result => {
        this.retrying = false;
        if (this.selectedBroadcast) {
          this.loadSummary(this.selectedBroadcast.id);
          this.loadRecipients(this.selectedBroadcast.id);
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.retrying = false;
        this.cdr.markForCheck();
      },
    });
  }

  private loadRecipients(broadcastId: number): void {
    this.recipientsLoading = true;
    this.broadcastService.getRecipients(broadcastId, this.recipientsPage, this.recipientsPageSize, this.statusFilter || undefined).subscribe({
      next: result => {
        this.recipients = result.items;
        this.recipientsTotalRecords = result.totalCount;
        this.recipientsLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.recipientsLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private loadSummary(broadcastId: number): void {
    this.broadcastService.getDeliverySummary(broadcastId).subscribe({
      next: s => {
        this.summary = s;
        this.cdr.markForCheck();
      },
      error: () => { /* silently ignore */ },
    });
  }
}
