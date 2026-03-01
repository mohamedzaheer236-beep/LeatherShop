export interface BroadcastRequest {
  templateName: string;
  languageCode: string;
  parameters: string[];
  imageUrl?: string;
  phoneNumbers?: string[];
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
