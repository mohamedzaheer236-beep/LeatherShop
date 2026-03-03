import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { BroadcastHistory } from '../../models/broadcast.model';

@Component({
  selector: 'app-broadcast-history',
  standalone: true,
  imports: [DatePipe, TableModule, TagModule, PaginatorModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './broadcast-history.component.html',
  styleUrl: './broadcast-history.component.scss',
})
export class BroadcastHistoryComponent {
  @Input() history: BroadcastHistory[] = [];
  @Input() totalRecords = 0;
  @Input() currentPage = 1;
  @Input() pageSize = 10;
  @Output() pageChange = new EventEmitter<PaginatorState>();

  onPageChange(event: PaginatorState): void {
    this.pageChange.emit(event);
  }
}
