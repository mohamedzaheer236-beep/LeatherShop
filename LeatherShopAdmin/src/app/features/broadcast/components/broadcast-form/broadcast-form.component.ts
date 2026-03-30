import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnDestroy, OnInit, Output, inject } from '@angular/core';

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
import { BroadcastService } from '../../services/broadcast.service';
import { BroadcastFormHelperService } from '../../services/broadcast-form-helper.service';
import { CarouselCard } from '../../models/broadcast.model';
import { ProductImageItem } from '../../../products/models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid as checkFieldInvalid } from '../../../../shared/utils/form.utils';
import { SignalRService, BroadcastProgressEvent } from '../../../../core/services/signalr.service';

import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

import { CUSTOMER_CATEGORIES } from '../../../customers/models/customer.model';

@Component({
  selector: 'app-broadcast-form',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, CardModule, DropdownModule, InputTextModule, ButtonModule],
  templateUrl: './broadcast-form.component.html',
  styleUrl: './broadcast-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [BroadcastFormHelperService],
})
export class BroadcastFormComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);
  private signalR = inject(SignalRService);
  readonly helper = inject(BroadcastFormHelperService);

  /** Emits when a broadcast has been sent (parent should refresh history). */
  @Output() sent = new EventEmitter<void>();

  broadcastForm!: FormGroup;
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';
  submitted = false;
  categoryOptions = [{ label: 'All Subscribers', value: '' }, ...CUSTOMER_CATEGORIES];

  // Progress tracking via SignalR
  broadcastProgress: BroadcastProgressEvent | null = null;
  private activeBroadcastId: number | null = null;
  private progressSub?: Subscription;

  private pollingSubs = new Map<number, Subscription>();

  ngOnInit(): void {
    this.initForm();
    this.helper.init();
  }

  ngOnDestroy(): void {
    this.pollingSubs.forEach(sub => sub.unsubscribe());
    this.pollingSubs.clear();
    this.progressSub?.unsubscribe();
  }

  // ─── Form Setup ───

  private initForm(): void {
    this.broadcastForm = this.fb.group({
      templateName: [null, [Validators.required, this.templateValidator.bind(this)]],
      parameters: [''],
      imageUrl: [''],
      category: [''],
    });
  }

  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    return this.helper.isValidTemplate(value) ? null : { invalidTemplate: true };
  }

  get f() {
    return this.broadcastForm.controls;
  }

  isFieldInvalid(field: string): boolean {
    return checkFieldInvalid(this.broadcastForm, field, this.submitted);
  }

  get isValidTemplate(): boolean {
    return this.helper.isValidTemplate(this.f['templateName'].value);
  }

  // ─── Template Selection ───

  onTemplateSelect(): void {
    this.f['templateName'].updateValueAndValidity();
    this.helper.applyTemplate(this.f['templateName'].value);
    if (!this.helper.hasImageHeader) {
      this.broadcastForm.patchValue({ imageUrl: '' });
    }
  }

  onDropdownHide(): void {
    this.f['templateName'].markAsTouched();
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
      params => this.broadcastForm.patchValue({ parameters: params }),
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

  // ─── Send Broadcast ───

  sendBroadcast(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.resultMessage = 'Please select a valid approved template!';
      this.resultType = 'error';
      return;
    }

    if (this.helper.isCarousel && !this.helper.carouselCardsValid) {
      this.resultMessage = 'Please upload images for all carousel cards.';
      this.resultType = 'error';
      return;
    }

    this.sending = true;
    this.resultMessage = '';

    const { templateName, parameters } = this.broadcastForm.value;
    const languageCode = this.helper.getLanguageCode(templateName);
    const category = this.broadcastForm.value.category || undefined;

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
          category,
        })
        .subscribe({
          next: res => {
            this.resultMessage = `Sending carousel to ${res.totalRecipients} subscribers...`;
            this.resultType = 'success';
            this.submitted = false;
            this.broadcastForm.reset();
            this.helper.reset();
            this.cdr.markForCheck();
            this.startTracking(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.resultMessage = 'Failed to send carousel broadcast. Check your template.';
            this.resultType = 'error';
            this.cdr.markForCheck();
          },
        });
    } else {
      // Use pre-split array when product is linked (descriptions may contain commas)
      const params = this.helper.linkedProductParams.length > 0
        ? this.helper.linkedProductParams
        : (parameters && parameters.trim() ? parameters.split(',').map((p: string) => p.trim()) : []);
      const imageUrl = this.broadcastForm.value.imageUrl;

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: params,
          imageUrl: imageUrl || undefined,
          category,
        })
        .subscribe({
          next: res => {
            this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
            this.resultType = 'success';
            this.submitted = false;
            this.broadcastForm.reset();
            this.helper.clearHeaderImage();
            this.cdr.markForCheck();
            this.startTracking(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.resultMessage = 'Failed to send broadcast. Check your template.';
            this.resultType = 'error';
            this.cdr.markForCheck();
          },
        });
    }
  }

  // ─── Real-time Progress via SignalR ───

  private startTracking(broadcastId: number, totalRecipients: number): void {
    this.activeBroadcastId = broadcastId;
    this.broadcastProgress = { broadcastId, sent: 0, failed: 0, total: totalRecipients, status: 'processing' };
    this.progressSub?.unsubscribe();

    this.progressSub = this.signalR.broadcastProgress$.subscribe(event => {
      if (event.broadcastId !== broadcastId) return;
      this.broadcastProgress = event;
      this.resultMessage = `Sending: ${event.sent} sent, ${event.failed} failed — ${event.sent + event.failed} of ${event.total}`;
      this.cdr.markForCheck();

      if (event.status === 'completed') {
        this.onBroadcastComplete(event);
      }
    });

    // Fallback: poll after 10s in case SignalR is disconnected
    const fallbackSub = this.broadcastService.pollBroadcastStatus(broadcastId, totalRecipients).subscribe({
      next: status => {
        // Only use fallback if SignalR hasn't delivered completion yet
        if (this.activeBroadcastId === broadcastId && this.broadcastProgress?.status !== 'completed') {
          this.onBroadcastComplete({
            broadcastId,
            sent: status.sentCount,
            failed: status.failedCount,
            total: totalRecipients,
            status: 'completed',
          });
        }
        this.pollingSubs.delete(broadcastId);
      },
      error: () => {
        this.pollingSubs.delete(broadcastId);
      },
    });
    this.pollingSubs.set(broadcastId, fallbackSub);
  }

  private onBroadcastComplete(event: BroadcastProgressEvent): void {
    this.progressSub?.unsubscribe();
    this.activeBroadcastId = null;
    this.sending = false;
    this.sent.emit();

    if (event.failed > 0 && event.sent === 0) {
      this.resultMessage = `Broadcast failed! ${event.failed} message(s) could not be delivered. Check if your template is approved.`;
      this.resultType = 'error';
      this.notification.error(`Broadcast failed for ${event.failed} recipient(s).`);
    } else if (event.failed > 0) {
      this.resultMessage = `Broadcast completed: ${event.sent} sent, ${event.failed} failed.`;
      this.resultType = 'success';
      this.notification.warning(`Broadcast: ${event.sent} sent, ${event.failed} failed.`);
    } else {
      this.resultMessage = `Broadcast successful! ${event.sent} message(s) delivered.`;
      this.resultType = 'success';
      this.notification.success(`Broadcast sent to ${event.sent} subscribers.`);
    }
    this.broadcastProgress = null;
    this.cdr.markForCheck();
  }
}
