import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { FormsModule } from '@angular/forms';
import { BroadcastHistory, BroadcastRecipient, BroadcastDeliverySummary, RetryAttemptEntry } from '../../models/broadcast.model';
import { BroadcastService } from '../../services/broadcast.service';

@Component({
  selector: 'app-broadcast-history',
  standalone: true,
  imports: [DatePipe, TableModule, TagModule, PaginatorModule, DialogModule, ButtonModule, DropdownModule, TooltipModule, OverlayPanelModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './broadcast-history.component.html',
  styleUrl: './broadcast-history.component.scss',
})
export class BroadcastHistoryComponent {
  private broadcastService = inject(BroadcastService);
  private cdr = inject(ChangeDetectorRef);

  @Input() history: BroadcastHistory[] = [];
  @Input() totalRecords = 0;
  @Input() currentPage = 1;
  @Input() pageSize = 10;
  @Output() pageChange = new EventEmitter<PaginatorState>();

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

  onPageChange(event: PaginatorState): void {
    this.pageChange.emit(event);
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
