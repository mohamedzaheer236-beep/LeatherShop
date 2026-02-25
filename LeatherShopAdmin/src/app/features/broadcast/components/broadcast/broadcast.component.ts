import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors
} from '@angular/forms';
import { BroadcastService } from '../../services/broadcast.service';
import { BroadcastHistory } from '../../models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';

import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';

@Component({
  selector: 'app-broadcast',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TagModule,
    ToolbarModule,
    DividerModule
  ],
  templateUrl: './broadcast.component.html',
  styleUrl: './broadcast.component.scss'
})
export class BroadcastComponent implements OnInit, OnDestroy {
  broadcastForm!: FormGroup;
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';
  submitted = false;

  history: BroadcastHistory[] = [];
  subscriberCount = 0;
  totalSent = 0;

  broadcastMode: 'custom' | 'template' = 'custom';
  customMessage = '';

  private pollingIntervals = new Map<number, ReturnType<typeof setInterval>>();

  constructor(
    private fb: FormBuilder,
    private broadcastService: BroadcastService,
    private notification: NotificationService,
    public templateLoader: TemplateLoaderService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.loadHistory();
    this.templateLoader.loadTemplates();
    this.broadcastService.getSubscriberCount().subscribe(data => {
      this.subscriberCount = data.subscriberCount;
    });
  }

  private initForm(): void {
    this.broadcastForm = this.fb.group({
      templateName: [null, [Validators.required, this.templateValidator.bind(this)]],
      parameters: [''],
      imageUrl: ['']
    });
  }

  /** Custom validator — checks if the selected template is approved */
  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null; // required validator handles empty
    if (!this.templateLoader.isValidTemplate(value)) {
      return { invalidTemplate: true };
    }
    return null;
  }

  get f() {
    return this.broadcastForm.controls;
  }

  onTemplateSelect(): void {
    this.f['templateName'].updateValueAndValidity();
  }

  /** Mark control touched when dropdown closes */
  onDropdownHide(): void {
    this.f['templateName'].markAsTouched();
  }

  get isValidTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.f['templateName'].value);
  }

  /** Helper: true when a field should show its error state */
  isFieldInvalid(field: string): boolean {
    const control = this.f[field];
    return control.invalid && (control.dirty || control.touched || this.submitted);
  }

  getResultSeverity(): 'success' | 'error' {
    return this.resultType === 'success' ? 'success' : 'error';
  }

  ngOnDestroy(): void {
    // Clear all active polling intervals
    this.pollingIntervals.forEach(interval => clearInterval(interval));
    this.pollingIntervals.clear();
  }

  loadHistory(): void {
    this.broadcastService.getBroadcastHistory().subscribe(data => {
      this.history = data;
      this.totalSent = data.reduce((sum, b) => sum + b.sentCount, 0);
    });
  }

  sendCustomMessage(): void {
    if (!this.customMessage.trim()) return;

    this.sending = true;
    this.resultMessage = '';

    this.broadcastService.sendBroadcast({
      templateName: 'shop_deals',
      languageCode: 'en',
      parameters: [this.customMessage.trim()]
    }).subscribe({
      next: (res) => {
        this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
        this.resultType = 'success';
        this.customMessage = '';
        this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
      },
      error: () => {
        this.sending = false;
        this.resultMessage = 'Failed to send broadcast. Make sure the shop_deals template is approved.';
        this.resultType = 'error';
      }
    });
  }

  private pollBroadcastStatus(broadcastId: number, totalRecipients: number): void {
    // If already polling this broadcast, skip
    if (this.pollingIntervals.has(broadcastId)) return;

    let attempts = 0;
    const maxAttempts = 30;
    const interval = setInterval(() => {
      attempts++;
      this.broadcastService.getBroadcastStatus(broadcastId).subscribe({
        next: (status) => {
          const processed = status.sentCount + status.failedCount;
          if (processed >= totalRecipients || attempts >= maxAttempts) {
            clearInterval(interval);
            this.pollingIntervals.delete(broadcastId);
            this.sending = this.pollingIntervals.size > 0; // still sending if other polls active
            this.loadHistory();
            if (status.failedCount > 0 && status.sentCount === 0) {
              this.resultMessage = `Broadcast failed! ${status.failedCount} message(s) could not be delivered. Check if your template is approved.`;
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
          }
        },
        error: () => {
          clearInterval(interval);
          this.pollingIntervals.delete(broadcastId);
          this.sending = this.pollingIntervals.size > 0;
          this.resultMessage = 'Could not verify broadcast delivery status.';
          this.resultType = 'error';
          this.loadHistory();
        }
      });
    }, 1000);
    this.pollingIntervals.set(broadcastId, interval);
  }

  sendBroadcast(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.resultMessage = 'Please select a valid approved template!';
      this.resultType = 'error';
      return;
    }

    this.sending = true;
    this.resultMessage = '';

    const { templateName, parameters, imageUrl } = this.broadcastForm.value;
    const params = parameters && parameters.trim()
      ? parameters.split(',').map((p: string) => p.trim())
      : [];

    const languageCode = this.templateLoader.getLanguageCode(templateName);

    this.broadcastService.sendBroadcast({
      templateName,
      languageCode,
      parameters: params,
      imageUrl: imageUrl || undefined
    }).subscribe({
      next: (res) => {
        this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
        this.resultType = 'success';
        this.submitted = false;
        this.broadcastForm.reset();
        this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
      },
      error: () => {
        this.sending = false;
        this.resultMessage = 'Failed to send broadcast. Check your template.';
        this.resultType = 'error';
      }
    });
  }
}