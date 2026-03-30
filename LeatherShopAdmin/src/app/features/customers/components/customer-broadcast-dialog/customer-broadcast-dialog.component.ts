import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, OnInit, OnDestroy, inject } from '@angular/core';

import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { Subscription } from 'rxjs';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { BroadcastFormHelperService } from '../../../broadcast/services/broadcast-form-helper.service';
import { CarouselCard } from '../../../broadcast/models/broadcast.model';
import { ProductImageItem } from '../../../products/models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid as checkFieldInvalid } from '../../../../shared/utils/form.utils';
import { SignalRService, BroadcastProgressEvent } from '../../../../core/services/signalr.service';

import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-broadcast-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, DialogModule, DropdownModule, InputTextModule, ButtonModule],
  templateUrl: './customer-broadcast-dialog.component.html',
  styleUrl: './customer-broadcast-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [BroadcastFormHelperService],
})
export class CustomerBroadcastDialogComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);
  private signalR = inject(SignalRService);
  readonly helper = inject(BroadcastFormHelperService);

  @Input() phoneNumbers: string[] = [];
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() sent = new EventEmitter<void>();

  broadcastForm!: FormGroup;
  sending = false;
  submitted = false;

  // Progress tracking
  broadcastProgress: BroadcastProgressEvent | null = null;
  sendResultMessage = '';
  private progressSub?: Subscription;
  private pollingSub?: Subscription;

  ngOnInit(): void {
    this.broadcastForm = this.fb.group({
      template: ['', [Validators.required, this.templateValidator.bind(this)]],
      params: [''],
      imageUrl: [''],
    });
    this.helper.init();
  }

  ngOnDestroy(): void {
    this.progressSub?.unsubscribe();
    this.pollingSub?.unsubscribe();
  }

  // ─── Template Selection ───

  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    return this.helper.isValidTemplate(value) ? null : { invalidTemplate: true };
  }

  get isValidTemplate(): boolean {
    return this.helper.isValidTemplate(this.broadcastForm.get('template')?.value);
  }

  onTemplateSelect(): void {
    const name = this.broadcastForm.get('template')?.value;
    this.broadcastForm.get('template')?.updateValueAndValidity();
    this.helper.applyTemplate(name);
    if (!this.helper.hasImageHeader) {
      this.broadcastForm.patchValue({ imageUrl: '' });
    }
  }

  // ─── Header Image ───

  onHeaderImageSelect(event: Event): void {
    this.helper.handleHeaderImageUpload(event, path => {
      this.broadcastForm.patchValue({ imageUrl: path });
    });
  }

  removeHeaderImage(): void {
    this.helper.clearHeaderImage();
    this.broadcastForm.patchValue({ imageUrl: '' });
  }

  // ─── Linked Product (standard template) ───

  onLinkedProductSelect(): void {
    this.helper.onLinkedProductSelect(
      params => this.broadcastForm.patchValue({ params }),
      path => this.broadcastForm.patchValue({ imageUrl: path })
    );
    this.cdr.markForCheck();
  }

  onLinkedImageSelect(img: ProductImageItem): void {
    this.helper.selectLinkedProductImage(img, path => {
      this.broadcastForm.patchValue({ imageUrl: path });
    });
    this.cdr.markForCheck();
  }

  // ─── Computed ───

  isFieldInvalid(field: string): boolean {
    return checkFieldInvalid(this.broadcastForm, field, this.submitted);
  }

  // ─── Send ───

  send(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.notification.error('Please select a valid approved template!');
      return;
    }

    if (this.helper.isCarousel && !this.helper.carouselCardsValid) {
      this.notification.error('Please select images for all carousel cards.');
      return;
    }

    if (this.phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }

    this.sending = true;
    const templateName = this.broadcastForm.get('template')?.value;
    const languageCode = this.helper.getLanguageCode(templateName);

    if (this.helper.isCarousel) {
      const cards: CarouselCard[] = this.helper.carouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload,
      }));
      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: [],
          isCarousel: true,
          carouselCards: cards,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.cdr.markForCheck();
          },
        });
    } else {
      const rawParams = this.broadcastForm.get('params')?.value || '';
      // Use pre-split array when product is linked (descriptions may contain commas)
      let params: string[];
      if (this.helper.linkedProductParams.length > 0) {
        params = this.helper.linkedProductParams;
      } else if (!rawParams.trim()) {
        params = [];
      } else if (this.helper.bodyParamCount <= 1) {
        // Single-parameter templates: send entire text as one param (may contain commas)
        params = [rawParams.trim()];
      } else {
        params = rawParams.split(',').map((p: string) => p.trim());
      }
      const imageUrl = this.broadcastForm.get('imageUrl')?.value;

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: params,
          imageUrl: imageUrl || undefined,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.cdr.markForCheck();
          },
        });
    }
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.submitted = false;
    this.broadcastForm.reset({ template: '', params: '', imageUrl: '' });
    this.helper.reset();
    this.broadcastProgress = null;
    this.sendResultMessage = '';
  }

  // ─── Private ───

  private onSendSuccess(broadcastId: number, totalRecipients: number): void {
    this.broadcastProgress = { broadcastId, sent: 0, failed: 0, total: totalRecipients, status: 'processing' };
    this.sendResultMessage = `Sending to ${totalRecipients} customer${totalRecipients === 1 ? '' : 's'}...`;
    this.cdr.markForCheck();

    this.progressSub?.unsubscribe();
    this.progressSub = this.signalR.broadcastProgress$.subscribe(event => {
      if (event.broadcastId !== broadcastId) return;
      this.broadcastProgress = event;
      this.sendResultMessage = `Sending: ${event.sent} sent, ${event.failed} failed — ${event.sent + event.failed} of ${event.total}`;
      this.cdr.markForCheck();

      if (event.status === 'completed') {
        this.onTrackingComplete(event);
      }
    });

    // Fallback polling
    this.pollingSub?.unsubscribe();
    this.pollingSub = this.broadcastService.pollBroadcastStatus(broadcastId, totalRecipients).subscribe({
      next: status => {
        if (this.broadcastProgress?.status !== 'completed') {
          this.onTrackingComplete({
            broadcastId,
            sent: status.sentCount,
            failed: status.failedCount,
            total: totalRecipients,
            status: 'completed',
          });
        }
      },
      error: () => { /* handled by SignalR */ },
    });
  }

  private onTrackingComplete(event: BroadcastProgressEvent): void {
    this.progressSub?.unsubscribe();
    this.pollingSub?.unsubscribe();
    this.sending = false;
    this.broadcastProgress = null;
    this.sendResultMessage = '';
    this.close();
    if (event.failed > 0 && event.sent > 0) {
      this.notification.warning(`Broadcast: ${event.sent} sent, ${event.failed} failed.`);
    } else if (event.failed > 0) {
      this.notification.error(`Broadcast failed for ${event.failed} recipient(s).`);
    } else {
      this.notification.success(`Broadcast sent to ${event.sent} customer${event.sent === 1 ? '' : 's'}.`);
    }
    this.sent.emit();
  }
}
