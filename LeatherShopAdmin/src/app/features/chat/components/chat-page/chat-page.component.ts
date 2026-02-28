import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { BadgeModule } from 'primeng/badge';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DialogModule } from 'primeng/dialog';
import { Subscription } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { Conversation, ChatMessage } from '../../models/chat.model';
import { SignalRService } from '../../../../core/services/signalr.service';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, ButtonModule, BadgeModule, TooltipModule, ProgressSpinnerModule, DialogModule],
  templateUrl: './chat-page.component.html',
  styleUrl: './chat-page.component.scss'
})
export class ChatPageComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef;

  conversations: Conversation[] = [];
  messages: ChatMessage[] = [];
  selectedCustomerId: number | null = null;
  selectedConversation: Conversation | null = null;
  searchTerm = '';
  messageText = '';
  sending = false;
  loadingConversations = false;
  loadingMessages = false;
  hasMoreMessages = false;
  currentPage = 1;
  showDeleteConversation = false;
  deletingConversation = false;

  private subs: Subscription[] = [];
  private shouldScrollToBottom = false;
  private searchTimeout: number | null = null;

  constructor(
    private chatService: ChatService,
    private signalR: SignalRService
  ) {}

  ngOnInit(): void {
    this.loadConversations();

    // Listen for real-time incoming messages for the active chat
    this.subs.push(
      this.signalR.chatMessage$.subscribe(msg => {
        // Only append messages that belong to the currently viewed conversation
        if (this.selectedCustomerId && msg.customerId === this.selectedCustomerId) {
          // Avoid duplicates (admin's own sent message already added optimistically)
          const exists = this.messages.some(m => m.id === msg.id);
          if (!exists) {
            this.messages.push({
              id: msg.id,
              direction: msg.direction,
              messageType: msg.messageType,
              content: msg.content,
              senderName: msg.senderName,
              isFromBot: msg.isFromBot,
              timestamp: msg.timestamp
            });
            this.shouldScrollToBottom = true;
          }
        }
      })
    );

    // Listen for new chat messages to refresh conversation list
    this.subs.push(
      this.signalR.newChatMessage$.subscribe(() => {
        this.loadConversations();
      })
    );
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  ngOnDestroy(): void {
    if (this.selectedCustomerId) {
      this.signalR.leaveCustomerChat(this.selectedCustomerId);
    }
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    this.subs.forEach(s => s.unsubscribe());
  }

  loadConversations(): void {
    this.loadingConversations = true;
    this.chatService.getConversations(this.searchTerm || undefined).subscribe({
      next: convs => {
        this.conversations = convs;
        // Update the selected conversation if still selected
        if (this.selectedCustomerId) {
          this.selectedConversation = convs.find(c => c.customerId === this.selectedCustomerId) || this.selectedConversation;
        }
        this.loadingConversations = false;
      },
      error: () => this.loadingConversations = false
    });
  }

  onSearch(): void {
    if (this.searchTimeout) clearTimeout(this.searchTimeout);
    this.searchTimeout = window.setTimeout(() => this.loadConversations(), 300);
  }

  selectConversation(conv: Conversation): void {
    // Leave previous chat group
    if (this.selectedCustomerId && this.selectedCustomerId !== conv.customerId) {
      this.signalR.leaveCustomerChat(this.selectedCustomerId);
    }

    this.selectedCustomerId = conv.customerId;
    this.selectedConversation = conv;
    this.messages = [];
    this.currentPage = 1;

    // Join SignalR group for this customer
    this.signalR.joinCustomerChat(conv.customerId);

    this.loadMessages();
  }

  loadMessages(): void {
    if (!this.selectedCustomerId) return;
    this.loadingMessages = true;
    this.chatService.getMessages(this.selectedCustomerId, this.currentPage).subscribe({
      next: result => {
        if (this.currentPage === 1) {
          this.messages = result.items;
          this.shouldScrollToBottom = true;
        } else {
          // Prepend older messages
          this.messages = [...result.items, ...this.messages];
        }
        this.hasMoreMessages = this.currentPage < result.totalPages;
        this.loadingMessages = false;
      },
      error: () => this.loadingMessages = false
    });
  }

  loadMoreMessages(): void {
    this.currentPage++;
    this.loadMessages();
  }

  sendMessage(): void {
    if (!this.selectedCustomerId || !this.messageText.trim() || this.sending) return;

    const text = this.messageText.trim();
    this.messageText = '';
    this.sending = true;

    this.chatService.sendMessage(this.selectedCustomerId, text).subscribe({
      next: (msg) => {
        // Add the sent message to the UI
        this.messages.push(msg);
        this.shouldScrollToBottom = true;
        this.sending = false;

        // Update conversation's bot pause state (sending auto-pauses)
        if (this.selectedConversation) {
          this.selectedConversation.isBotPaused = true;
        }
      },
      error: () => {
        this.messageText = text; // Restore on error
        this.sending = false;
      }
    });
  }

  toggleBot(): void {
    if (!this.selectedCustomerId) return;
    this.chatService.toggleBot(this.selectedCustomerId).subscribe({
      next: (result) => {
        if (this.selectedConversation) {
          this.selectedConversation.isBotPaused = result.isBotPaused;
        }
      },
      error: () => { /* Toast shown by error interceptor */ }
    });
  }

  confirmDeleteConversation(): void {
    this.showDeleteConversation = true;
  }

  deleteConversation(): void {
    if (!this.selectedCustomerId) return;
    this.deletingConversation = true;
    this.chatService.deleteConversation(this.selectedCustomerId).subscribe({
      next: () => {
        this.deletingConversation = false;
        this.showDeleteConversation = false;
        this.signalR.leaveCustomerChat(this.selectedCustomerId!);
        this.selectedCustomerId = null;
        this.selectedConversation = null;
        this.messages = [];
        this.loadConversations();
      },
      error: () => {
        this.deletingConversation = false;
      }
    });
  }

  formatMessage(content: string): string {
    // Convert *bold* to <strong>bold</strong> and preserve newlines
    return content
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/\*([^*]+)\*/g, '<strong>$1</strong>')
      .replace(/\n/g, '<br>');
  }

  formatTime(timestamp: string): string {
    const date = new Date(timestamp);
    const now = new Date();
    const diffDays = Math.floor((now.getTime() - date.getTime()) / 86400000);
    if (diffDays === 0) return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return date.toLocaleDateString([], { weekday: 'short' });
    return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
  }

  formatMessageTime(timestamp: string): string {
    return new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  trackByConversation(_index: number, conv: Conversation): number {
    return conv.customerId;
  }

  trackByMessage(_index: number, msg: ChatMessage): number {
    return msg.id;
  }

  private scrollToBottom(): void {
    try {
      if (this.messagesContainer) {
        this.messagesContainer.nativeElement.scrollTop = this.messagesContainer.nativeElement.scrollHeight;
      }
    } catch {
      // Intentionally empty — scrolling is a best-effort UI enhancement
    }
  }
}
