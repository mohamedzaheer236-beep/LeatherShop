export interface BroadcastRequest {
  templateName: string;
  languageCode: string;
  parameters: string[];
  imageUrl?: string;
  phoneNumbers?: string[];
  category?: string;
  isCarousel?: boolean;
  carouselCards?: CarouselCard[];
}

export interface CarouselCard {
  imageUrl: string;
  bodyParam: string;
  buttonPayload: string;
}

/** UI state for a single carousel card in the broadcast form */
export interface CarouselCardUI {
  imageUrl: string;
  imagePreview: string | null;
  bodyParam: string;
  buttonPayload: string;
  selectedProductId: number | null;
  selectedImageId: number | null;
  uploading: boolean;
}

export interface BroadcastResult {
  message: string;
  broadcastId: number;
  totalRecipients: number;
}

export interface BroadcastHistory {
  id: number;
  messageTemplate: string;
  messageBody: string;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  deliveredCount: number;
  readCount: number;
  sentAt: string;
  status: string;
  isCarousel: boolean;
}

export interface WhatsAppTemplate {
  name: string;
  language: string;
  status: string;
  category: string;
  isCarousel: boolean;
  cardCount: number;
  hasImageHeader: boolean;
  bodyParamCount: number;
  cardBodyMaxLength: number;
}

export interface BroadcastRecipient {
  id: number;
  phone: string;
  status: string;
  errorDetail: string | null;
  createdAt: string;
  sentAt: string | null;
  deliveredAt: string | null;
  readAt: string | null;
  failedAt: string | null;
  retryCount: number;
  nextRetryAt: string | null;
}

export interface BroadcastDeliverySummary {
  totalRecipients: number;
  queued: number;
  sent: number;
  delivered: number;
  read: number;
  failed: number;
  retryScheduled: number;
}

export interface BroadcastRetryResult {
  scheduledCount: number;
  message: string;
}
