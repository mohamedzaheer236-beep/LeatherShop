export interface Conversation {
  customerId: number;
  customerName: string;
  phoneNumber: string;
  lastMessage: string;
  lastMessageAt: string;
  unreadCount: number;
  isBotPaused: boolean;
}

export interface ChatMessage {
  id: number;
  direction: string; // 'Incoming' | 'Outgoing'
  messageType: string;
  content: string;
  senderName: string;
  isFromBot: boolean;
  timestamp: string;
}

export interface PaginatedMessages {
  items: ChatMessage[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FailedOutboxMessage {
  id: number;
  to: string;
  customerName: string;
  context: string;
  contentPreview: string;
  retryCount: number;
  lastError: string;
  createdAt: string;
}
