import { Pipe, PipeTransform } from '@angular/core';

/**
 * Impure pipe that formats a timestamp into a relative/short time label.
 * Used in notification list sidebar. Impure so it auto-refreshes as time passes.
 */
@Pipe({ name: 'timeAgo', standalone: true, pure: false })
export class TimeAgoPipe implements PipeTransform {
  transform(timestamp: string | null | undefined): string {
    if (!timestamp) return '';
    const diff = Date.now() - new Date(timestamp).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'Just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
  }
}

/**
 * Pure pipe that formats a timestamp into a short date label for conversations.
 * Shows time for today, "Yesterday", weekday for <7 days, date for older.
 */
@Pipe({ name: 'conversationTime', standalone: true, pure: true })
export class ConversationTimePipe implements PipeTransform {
  transform(timestamp: string | null | undefined): string {
    if (!timestamp) return '';
    const date = new Date(timestamp);
    const now = new Date();

    // Compare calendar dates (not elapsed time) so 23:55 → 00:05 correctly shows "Yesterday"
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const diffDays = Math.round((startOfToday.getTime() - startOfDate.getTime()) / 86400000);

    if (diffDays === 0) return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return date.toLocaleDateString([], { weekday: 'short' });
    return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
  }
}

/**
 * Pure pipe that formats a timestamp to HH:MM for chat message bubbles.
 */
@Pipe({ name: 'messageTime', standalone: true, pure: true })
export class MessageTimePipe implements PipeTransform {
  transform(timestamp: string | null | undefined): string {
    if (!timestamp) return '';
    return new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
