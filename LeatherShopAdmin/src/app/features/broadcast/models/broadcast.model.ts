export interface BroadcastRequest {
  templateName: string;
  languageCode: string;
  parameters: string[];
  imageUrl?: string;
  phoneNumbers?: string[];
}

export interface BroadcastHistory {
  id: number;
  messageTemplate: string;
  messageBody: string;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  sentAt: string;
}

export interface WhatsAppTemplate {
  name: string;
  language: string;
  status: string;
  category: string;
}
