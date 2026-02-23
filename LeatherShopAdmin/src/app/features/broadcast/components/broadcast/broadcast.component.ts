import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
    FormsModule,
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
  templateName = '';
  languageCode = '';
  parameters = '';
  imageUrl = '';
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';

  history: BroadcastHistory[] = [];
  subscriberCount = 0;

  constructor(
    private broadcastService: BroadcastService,
    private notification: NotificationService,
    public templateLoader: TemplateLoaderService
  ) {}

  ngOnInit(): void {
    this.loadHistory();
    this.templateLoader.loadTemplates();
    this.broadcastService.getSubscriberCount().subscribe(data => {
      this.subscriberCount = data.subscriberCount;
    });
  }

  onTemplateSelect(): void {
    this.languageCode = this.templateLoader.getLanguageCode(this.templateName);
  }

  get isValidTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.templateName);
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