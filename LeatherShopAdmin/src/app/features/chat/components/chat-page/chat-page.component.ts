import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewChecked, inject } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { BadgeModule } from 'primeng/badge';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { Subscription } from 'rxjs';
import { ChatService } from '../../services/chat.service';
import { Conversation, ChatMessage, FailedOutboxMessage } from '../../models/chat.model';
import { SignalRService } from '../../../../core/services/signalr.service';
import { FormatMessagePipe } from '../../../../shared/pipes/format-message.pipe';
import { ConversationTimePipe, MessageTimePipe, DateSeparatorPipe } from '../../../../shared/pipes/time.pipes';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [
    FormsModule,
    InputTextModule,
    ButtonModule,
    BadgeModule,
    TooltipModule,
    ProgressSpinnerModule,
    DialogModule,
    TagModule,
    FormatMessagePipe,
    ConversationTimePipe,
    MessageTimePipe,
    DateSeparatorPipe,
  ],
  templateUrl: './chat-page.component.html',
  styleUrl: './chat-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatPageComponent implements OnInit, OnDestroy, AfterViewChecked {
  private chatService = inject(ChatService);
  private signalR = inject(SignalRService);
  private cdr = inject(ChangeDetectorRef);

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

  // Failed outbox messages
  failedMessages: FailedOutboxMessage[] = [];
  failedCount = 0;
  showFailedMessages = false;
  retryingId: number | null = null;

  private subs: Subscription[] = [];
  private shouldScrollToBottom = false;

  private searchTimeout: number | null = null;
  private conversationRefreshTimeout: number | null = null;
  private scrollRestorationTimeout: number | null = null;

  ngOnInit(): void {
    this.loadConversations();
    this.loadFailedMessageCount();

    // Listen for real-time incoming messages for the active chat
    this.subs.push(
      this.signalR.chatMessage$.subscribe(msg => {
        // F78 fix: Guard against stale messages from a previously-selected conversation
        // (can happen if conversation was switched while a SignalR message was in flight)
        if (this.selectedCustomerId && msg.customerId === this.selectedCustomerId) {
          // Avoid duplicates (admin's own sent message already added optimistically)
          const exists = this.messages.some(m => m.id === msg.id);
          if (!exists) {
            this.messages = [...this.messages, {
              id: msg.id,
              direction: msg.direction as ChatMessage['direction'],
              messageType: msg.messageType,
              content: msg.content,
              senderName: msg.senderName,
              isFromBot: msg.isFromBot,
              timestamp: msg.timestamp,
            }];
            this.shouldScrollToBottom = true;
            this.cdr.markForCheck();
          }
        }
      }),
    );

    // Listen for new chat messages to refresh conversation list (debounced — F79 fix)
    this.subs.push(
      this.signalR.newChatMessage$.subscribe(() => {
        this.debouncedLoadConversations();
      }),
    );

    // Listen for outbox message failures — update badge count in real time
    this.subs.push(
      this.signalR.outboxFailed$.subscribe(() => {
        this.loadFailedMessageCount();
        // If the dialog is open, refresh the list too
        if (this.showFailedMessages) {
          this.loadFailedMessages();
        }
      }),
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
    if (this.conversationRefreshTimeout) {
      clearTimeout(this.conversationRefreshTimeout);
    }
    if (this.scrollRestorationTimeout) {
      clearTimeout(this.scrollRestorationTimeout);
    }
    this.subs.forEach(s => s.unsubscribe());
  }

  /** F79 fix: Debounce conversation list refreshes to avoid spamming the API on rapid SignalR events */
  private debouncedLoadConversations(): void {
    if (this.conversationRefreshTimeout) {
      clearTimeout(this.conversationRefreshTimeout);
    }
    this.conversationRefreshTimeout = window.setTimeout(() => {
      this.loadConversations();
      this.conversationRefreshTimeout = null;
      this.cdr.markForCheck();
    }, 500);
  }

  loadConversations(): void {
    this.loadingConversations = true;
    this.chatService.getConversations(this.searchTerm || undefined).subscribe({
      next: convs => {
        this.conversations = convs;
        // Update the selected conversation if still selected
        if (this.selectedCustomerId) {
          this.selectedConversation =
            convs.find(c => c.customerId === this.selectedCustomerId) || this.selectedConversation;
        }
        this.loadingConversations = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loadingConversations = false;
        this.cdr.markForCheck();
      },
    });
  }

  onSearch(): void {
    if (this.searchTimeout) clearTimeout(this.searchTimeout);
    this.searchTimeout = window.setTimeout(() => {
      this.loadConversations();
      this.cdr.markForCheck();
    }, 300);
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
    this.sending = false;

    // Join SignalR group for this customer
    this.signalR.joinCustomerChat(conv.customerId);

    this.loadMessages();
  }

  loadMessages(): void {
    if (!this.selectedCustomerId) return;
    // F78 fix: capture the customer ID at call time to detect stale responses
    const requestedCustomerId = this.selectedCustomerId;
    this.loadingMessages = true;
    this.chatService.getMessages(this.selectedCustomerId, this.currentPage).subscribe({
      next: result => {
        // F78 fix: discard response if user switched conversations while loading
        if (this.selectedCustomerId !== requestedCustomerId) {
          this.loadingMessages = false;
          this.cdr.markForCheck();
          return;
        }

        if (this.currentPage === 1) {
          this.messages = result.items;
          this.shouldScrollToBottom = true;
          this.hasMoreMessages = this.currentPage < result.totalPages;
          this.loadingMessages = false;
          this.cdr.markForCheck();
        } else {
          // Preserve scroll position when prepending older messages.
          // Keep loadingMessages=true until scroll is restored to prevent
          // the scroll handler from firing a runaway loop.
          const container = this.messagesContainer?.nativeElement;
          const previousScrollHeight = container?.scrollHeight ?? 0;
          this.messages = [...result.items, ...this.messages];
          this.hasMoreMessages = this.currentPage < result.totalPages;
          this.cdr.markForCheck();
          // Restore scroll position after DOM update, then unlock
          this.scrollRestorationTimeout = window.setTimeout(() => {
            if (container) {
              container.scrollTop = container.scrollHeight - previousScrollHeight;
            }
            this.loadingMessages = false;
            this.cdr.markForCheck();
          });
        }
      },
      error: () => {
        this.loadingMessages = false;
        this.cdr.markForCheck();
      },
    });
  }

  loadMoreMessages(): void {
    this.currentPage++;
    this.loadMessages();
  }

  sendMessage(): void {
    if (!this.selectedCustomerId || !this.messageText.trim() || this.sending) return;

    const text = this.messageText.trim();
    const targetCustomerId = this.selectedCustomerId;
    this.messageText = '';
    this.sending = true;

    this.chatService.sendMessage(targetCustomerId, text).subscribe({
      next: msg => {
        // Guard: discard if user switched conversations while send was in-flight
        if (this.selectedCustomerId !== targetCustomerId) {
          this.sending = false;
          return;
        }
        // Add the sent message to the UI
        this.messages = [...this.messages, msg];
        this.shouldScrollToBottom = true;
        this.sending = false;

        // Update conversation's bot pause state (sending auto-pauses)
        if (this.selectedConversation) {
          this.selectedConversation.isBotPaused = true;
        }
        this.cdr.markForCheck();
      },
      error: () => {
        // Only restore text if still on same conversation
        if (this.selectedCustomerId === targetCustomerId) {
          this.messageText = text;
        }
        this.sending = false;
        this.cdr.markForCheck();
      },
    });
  }

  toggleBot(): void {
    if (!this.selectedCustomerId) return;
    this.chatService.toggleBot(this.selectedCustomerId).subscribe({
      next: result => {
        if (this.selectedConversation) {
          this.selectedConversation.isBotPaused = result.isBotPaused;
        }
        this.cdr.markForCheck();
      },
      error: () => {
        /* Toast shown by error interceptor */
      },
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
        this.cdr.markForCheck();
      },
      error: () => {
        this.deletingConversation = false;
        this.cdr.markForCheck();
      },
    });
  }

  trackByConversation(_index: number, conv: Conversation): number {
    return conv.customerId;
  }

  trackByMessage(_index: number, msg: ChatMessage): number {
    return msg.id;
  }

  /** Returns true if this message is the first of its calendar day (needs a date separator above it). */
  showDateSeparator(index: number): boolean {
    if (index === 0) return true;
    const curr = new Date(this.messages[index].timestamp);
    const prev = new Date(this.messages[index - 1].timestamp);
    return curr.toDateString() !== prev.toDateString();
  }

  trackByFailedMessage(_index: number, msg: FailedOutboxMessage): number {
    return msg.id;
  }

  // ── Failed outbox messages ──

  loadFailedMessageCount(): void {
    this.chatService.getFailedMessageCount().subscribe({
      next: count => {
        this.failedCount = count;
        this.cdr.markForCheck();
      },
      error: () => {
        /* Silent — badge is optional */
      },
    });
  }

  openFailedMessages(): void {
    this.showFailedMessages = true;
    this.loadFailedMessages();
  }

  loadFailedMessages(): void {
    this.chatService.getFailedMessages().subscribe({
      next: msgs => {
        this.failedMessages = msgs;
        this.cdr.markForCheck();
      },
      error: () => {
        /* Toast shown by error interceptor */
      },
    });
  }

  retryMessage(msg: FailedOutboxMessage): void {
    this.retryingId = msg.id;
    this.chatService.retryOutboxMessage(msg.id).subscribe({
      next: () => {
        // Remove from list and update count
        this.failedMessages = this.failedMessages.filter(m => m.id !== msg.id);
        this.failedCount = Math.max(0, this.failedCount - 1);
        this.retryingId = null;
        if (this.failedMessages.length === 0) {
          this.showFailedMessages = false;
        }
        this.cdr.markForCheck();
      },
      error: () => {
        this.retryingId = null;
        this.cdr.markForCheck();
      },
    });
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
