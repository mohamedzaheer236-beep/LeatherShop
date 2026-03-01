import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pure pipe that converts WhatsApp-style markdown to safe HTML.
 * Replaces the formatMessage() method call in templates — pure pipes
 * memoize output and only re-evaluate when the input reference changes,
 * avoiding regex execution on every change-detection cycle.
 */
@Pipe({ name: 'formatMessage', standalone: true, pure: true })
export class FormatMessagePipe implements PipeTransform {
  transform(content: string | null | undefined): string {
    if (!content) return '';
    return content
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/\*([^*]+)\*/g, '<strong>$1</strong>')
      .replace(/_([^_]+)_/g, '<em>$1</em>')
      .replace(/~([^~]+)~/g, '<del>$1</del>')
      .replace(/\n/g, '<br>');
  }
}
