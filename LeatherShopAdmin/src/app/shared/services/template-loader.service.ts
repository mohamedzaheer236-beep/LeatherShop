import { Injectable } from '@angular/core';
import { Subscription } from 'rxjs';
import { BroadcastService } from '../../features/broadcast/services/broadcast.service';
import { WhatsAppTemplate } from '../../features/broadcast/models/broadcast.model';

export interface TemplateLoaderState {
  templates: WhatsAppTemplate[];
  templateOptions: { label: string; value: string }[];
  loadingTemplates: boolean;
  templatesLoaded: boolean;
}

/**
 * Shared service for loading and validating WhatsApp templates.
 * Used by both BroadcastComponent and CustomersComponent to avoid code duplication.
 */
@Injectable({ providedIn: 'root' })
export class TemplateLoaderService {
  private state: TemplateLoaderState = {
    templates: [],
    templateOptions: [],
    loadingTemplates: false,
    templatesLoaded: false
  };

  private loaded = false;
  private loadSub?: Subscription;

  constructor(private broadcastService: BroadcastService) {}

  getState(): TemplateLoaderState {
    return this.state;
  }

  loadTemplates(forceReload = false): void {
    if (this.loaded && !forceReload) return;
    // Prevent duplicate concurrent HTTP requests
    if (this.state.loadingTemplates && !forceReload) return;

    // Cancel any in-flight request on forceReload to prevent stale data race
    this.loadSub?.unsubscribe();

    this.state.loadingTemplates = true;
    this.loadSub = this.broadcastService.getApprovedTemplates().subscribe({
      next: (data) => {
        this.state.templates = data;
        // Only show MARKETING templates in broadcast dropdown — UTILITY templates
        // (e.g., order_update) are for transactional messages, not broadcasts
        const marketingTemplates = data.filter(t => t.category === 'MARKETING');
        this.state.templateOptions = marketingTemplates.map(t => ({
          label: `${t.name} (${t.language})`,
          value: t.name
        }));
        this.state.templatesLoaded = true;
        this.state.loadingTemplates = false;
        this.loaded = true;
      },
      error: () => {
        this.state.templatesLoaded = true;
        this.state.loadingTemplates = false;
        // Don't set this.loaded = true — allow next navigation to retry on transient failures
      }
    });
  }

  isValidTemplate(templateName: string): boolean {
    if (!templateName.trim()) return false;
    if (this.state.templatesLoaded && this.state.templates.length > 0) {
      return this.state.templates.some(t => t.name === templateName);
    }
    return true;
  }

  getLanguageCode(templateName: string): string {
    const selected = this.state.templates.find(t => t.name === templateName);
    return selected ? selected.language : 'en_US';
  }

  getTemplate(templateName: string): WhatsAppTemplate | undefined {
    return this.state.templates.find(t => t.name === templateName);
  }

  isCarouselTemplate(templateName: string): boolean {
    const t = this.state.templates.find(tpl => tpl.name === templateName);
    return t?.isCarousel ?? false;
  }

  getCardCount(templateName: string): number {
    const t = this.state.templates.find(tpl => tpl.name === templateName);
    return t?.cardCount ?? 0;
  }
}
