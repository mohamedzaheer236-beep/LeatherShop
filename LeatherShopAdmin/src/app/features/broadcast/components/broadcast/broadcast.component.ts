import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, OnDestroy, inject } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { PaginatorState } from 'primeng/paginator';
import { BroadcastService } from '../../services/broadcast.service';
import { BroadcastHistory } from '../../models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { CustomerService } from '../../../customers/services/customer.service';

import { CardModule } from 'primeng/card';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';

import { BroadcastFormComponent } from '../broadcast-form/broadcast-form.component';
import { BroadcastHistoryComponent } from '../broadcast-history/broadcast-history.component';

@Component({
  selector: 'app-broadcast',
  standalone: true,
  imports: [
    FormsModule,
    CardModule,
    InputTextareaModule,
    ButtonModule,
    TagModule,
    ToolbarModule,
    DividerModule,
    BroadcastFormComponent,
    BroadcastHistoryComponent,
  ],
  templateUrl: './broadcast.component.html',
  styleUrl: './broadcast.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BroadcastComponent implements OnInit, OnDestroy {
  private broadcastService = inject(BroadcastService);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private templateLoader = inject(TemplateLoaderService);
  private cdr = inject(ChangeDetectorRef);

  history: BroadcastHistory[] = [];
  subscriberCount = 0;
  totalSent = 0;

  // History pagination
  historyTotalRecords = 0;
  historyCurrentPage = 1;
  historyPageSize = 10;

  broadcastMode: 'custom' | 'template' = 'custom';
  customMessage = '';

  // Quick message state
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';

  private pollingIntervals = new Map<number, ReturnType<typeof setInterval>>();

  ngOnInit(): void {
    this.loadHistory();
    this.customerService.getSubscriberCount().subscribe({
      next: data => {
        this.subscriberCount = data.subscriberCount;
        this.cdr.markForCheck();
      },
      error: () => {
        /* silently ignore */
      },
    });
    this.broadcastService.getTotalSentCount().subscribe({
      next: count => {
        this.totalSent = count;
        this.cdr.markForCheck();
      },
      error: () => {
        /* silently ignore */
      },
    });
  }

  ngOnDestroy(): void {
    this.pollingIntervals.forEach(interval => clearInterval(interval));
    this.pollingIntervals.clear();
  }

  // ─── History ───

  loadHistory(): void {
    this.broadcastService.getBroadcastHistory(this.historyCurrentPage, this.historyPageSize).subscribe({
      next: result => {
        this.history = result.items;
        this.historyTotalRecords = result.totalCount;
        this.cdr.markForCheck();
      },
      error: () => {
        /* silently ignore */
      },
    });
  }

  onHistoryPageChange(event: PaginatorState): void {
    this.historyCurrentPage = (event.page ?? 0) + 1;
    this.historyPageSize = event.rows ?? this.historyPageSize;
    this.loadHistory();
  }

  /** Called by BroadcastFormComponent when a template broadcast finishes */
  onBroadcastSent(): void {
    this.loadHistory();
    this.broadcastService.getTotalSentCount().subscribe({
      next: count => {
        this.totalSent = count;
        this.cdr.markForCheck();
      },
      error: () => {
        /* silently ignore */
      },
    });
  }

  // ─── Quick Custom Message ───

  sendCustomMessage(): void {
    if (!this.customMessage.trim()) return;

    this.sending = true;
    this.resultMessage = '';

    this.broadcastService
      .sendBroadcast({
        templateName: 'shop_deals',
        languageCode: this.templateLoader.getLanguageCode('shop_deals') || 'en_US',
        parameters: [this.customMessage.trim()],
      })
      .subscribe({
        next: res => {
          this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
          this.resultType = 'success';
          this.customMessage = '';
          this.cdr.markForCheck();
          this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
        },
        error: () => {
          this.sending = false;
          this.resultMessage = 'Failed to send broadcast. Make sure the shop_deals template is approved.';
          this.resultType = 'error';
          this.cdr.markForCheck();
        },
      });
  }

  private pollBroadcastStatus(broadcastId: number, totalRecipients: number): void {
    if (this.pollingIntervals.has(broadcastId)) return;

    let attempts = 0;
    const maxAttempts = 30;
    const interval = setInterval(() => {
      attempts++;
      this.broadcastService.getBroadcastStatus(broadcastId).subscribe({
        next: status => {
          const processed = status.sentCount + status.failedCount;
          if (processed >= totalRecipients || attempts >= maxAttempts) {
            clearInterval(interval);
            this.pollingIntervals.delete(broadcastId);
            this.sending = this.pollingIntervals.size > 0;
            this.loadHistory();
            if (status.failedCount > 0 && status.sentCount === 0) {
              this.resultMessage = `Broadcast failed! ${status.failedCount} message(s) could not be delivered.`;
              this.resultType = 'error';
              this.notification.error(`Broadcast failed for ${status.failedCount} recipient(s).`);
            } else if (status.failedCount > 0) {
              this.resultMessage = `Broadcast completed: ${status.sentCount} sent, ${status.failedCount} failed.`;
              this.resultType = 'success';
              this.notification.warning(`Broadcast: ${status.sentCount} sent, ${status.failedCount} failed.`);
            } else {
              this.resultMessage = `Broadcast successful! ${status.sentCount} message(s) delivered.`;
              this.resultType = 'success';
              this.notification.success(`Broadcast sent to ${status.sentCount} subscribers.`);
            }
            this.cdr.markForCheck();
          }
        },
        error: () => {
          clearInterval(interval);
          this.pollingIntervals.delete(broadcastId);
          this.sending = this.pollingIntervals.size > 0;
          this.resultMessage = 'Could not verify broadcast delivery status.';
          this.resultType = 'error';
          this.loadHistory();
          this.cdr.markForCheck();
        },
      });
    }, 1000);
    this.pollingIntervals.set(broadcastId, interval);
  }
}
