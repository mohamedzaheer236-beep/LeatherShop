import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
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
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DividerModule } from 'primeng/divider';

@Component({
  selector: 'app-broadcast',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    ButtonModule,
    TableModule,
    TagModule,
    ToolbarModule,
    MessageModule,
    ProgressSpinnerModule,
    DividerModule
  ],
  templateUrl: './broadcast.component.html',
  styleUrl: './broadcast.component.scss'
})
export class BroadcastComponent implements OnInit {
  broadcastForm!: FormGroup;
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';
  submitted = false;

  history: BroadcastHistory[] = [];
  subscriberCount = 0;

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

  getTotalSent(): number {
    return this.history.reduce((sum, b) => sum + b.sentCount, 0);
  }

  loadHistory(): void {
    this.broadcastService.getBroadcastHistory().subscribe(data => this.history = data);
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
        this.sending = false;
        this.resultMessage = `Broadcast started! Sending to ${res.totalRecipients} subscribers.`;
        this.resultType = 'success';
        this.notification.success(`Broadcast sent to ${res.totalRecipients} subscribers.`);
        this.submitted = false;
        this.broadcastForm.reset();
        this.loadHistory();
      },
      error: () => {
        this.sending = false;
        this.resultMessage = 'Failed to send broadcast. Check your template.';
        this.resultType = 'error';
      }
    });
  }
}