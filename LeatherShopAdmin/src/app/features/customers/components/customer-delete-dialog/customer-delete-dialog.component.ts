import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CustomerService } from '../../services/customer.service';
import { Customer } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';

import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-delete-dialog',
  standalone: true,
  imports: [DialogModule, ButtonModule],
  templateUrl: './customer-delete-dialog.component.html',
  styleUrl: './customer-delete-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerDeleteDialogComponent {
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);

  @Input() visible = false;
  @Input() customer: Customer | null = null;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() deleted = new EventEmitter<void>();

  deleting = false;

  confirm(): void {
    if (!this.customer) return;
    this.deleting = true;
    this.customerService.deleteCustomer(this.customer.id).subscribe({
      next: () => {
        this.deleting = false;
        this.close();
        this.notification.success('Customer deleted successfully!');
        this.deleted.emit();
        this.cdr.markForCheck();
      },
      error: () => {
        this.deleting = false;
        this.close();
        this.cdr.markForCheck();
      },
    });
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
