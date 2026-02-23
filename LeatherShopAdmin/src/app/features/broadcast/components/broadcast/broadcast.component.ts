import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BroadcastService } from '../../services/broadcast.service';
import { BroadcastHistory, WhatsAppTemplate } from '../../models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';

import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-broadcast',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    ButtonModule,
    TableModule,
    TagModule,
    ToolbarModule,
    MessageModule,
    ProgressSpinnerModule
  ],
  templateUrl: './broadcast.component.html',
  styleUrl: './broadcast.component.scss'
})
export class BroadcastComponent implements OnInit {
  templateName = '';
  languageCode = '';
  parameters = '';
  imageUrl = '';
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';

  history: BroadcastHistory[] = [];
  subscriberCount = 0;

  // Templates
  templates: WhatsAppTemplate[] = [];
  templateOptions: { label: string; value: string }[] = [];
  loadingTemplates = false;
  templatesLoaded = false;

  constructor(
    private broadcastService: BroadcastService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadHistory();
    this.loadTemplates();
    this.broadcastService.getSubscriberCount().subscribe(data => {
      this.subscriberCount = data.subscriberCount;
    });
  }

  loadTemplates(): void {
    this.loadingTemplates = true;
    this.broadcastService.getApprovedTemplates().subscribe({
      next: (data) => {
        this.templates = data;
        this.templateOptions = data.map(t => ({
          label: `${t.name} (${t.language}) - ${t.category}`,
          value: t.name
        }));
        this.templatesLoaded = true;
        this.loadingTemplates = false;
      },
      error: () => {
        this.templatesLoaded = true;
        this.loadingTemplates = false;
      }
    });
  }

  onTemplateSelect(): void {
    const selected = this.templates.find(t => t.name === this.templateName);
    if (selected) {
      this.languageCode = selected.language;
    } else {
      this.languageCode = 'en_US';
    }
  }

  get isValidTemplate(): boolean {
    if (!this.templateName.trim()) return false;
    if (this.templatesLoaded && this.templates.length > 0) {
      return this.templates.some(t => t.name === this.templateName);
    }
    return true;
  }

  getResultSeverity(): 'success' | 'error' {
    return this.resultType === 'success' ? 'success' : 'error';
  }

  loadHistory(): void {
    this.broadcastService.getBroadcastHistory().subscribe(data => this.history = data);
  }

  sendBroadcast(): void {
    if (!this.isValidTemplate) {
      this.resultMessage = 'Please select a valid approved template!';
      this.resultType = 'error';
      return;
    }

    this.sending = true;
    this.resultMessage = '';

    const params = this.parameters.trim()
      ? this.parameters.split(',').map(p => p.trim())
      : [];

    this.broadcastService.sendBroadcast({
      templateName: this.templateName,
      languageCode: this.languageCode,
      parameters: params,
      imageUrl: this.imageUrl || undefined
    }).subscribe({
      next: (res) => {
        this.sending = false;
        this.resultMessage = `Broadcast started! Sending to ${res.totalRecipients} subscribers.`;
        this.resultType = 'success';
        this.notification.success(`Broadcast sent to ${res.totalRecipients} subscribers.`);
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