import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { Customer, UpdateCustomer } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid } from '../../../../shared/utils/form.utils';

import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-edit-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DialogModule, InputTextModule, InputTextareaModule, CheckboxModule, ButtonModule],
  templateUrl: './customer-edit-dialog.component.html',
  styleUrl: './customer-edit-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerEditDialogComponent {
  private fb = inject(FormBuilder);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);

  @Input() visible = false;
  @Input() customer: Customer | null = null;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();

  form = this.fb.group({
    name: [''],
    address: ['', [Validators.required, Validators.minLength(10)]],
    isSubscribed: [true],
  });

  submitting = false;
  submitted = false;

  onShow(): void {
    this.submitted = false;
    this.form.reset({
      name: this.customer?.name || '',
      address: this.customer?.address || '',
      isSubscribed: this.customer?.isSubscribed ?? true,
    });
  }

  submit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (this.form.invalid || !this.customer) {
      this.notification.error('Please fill in all required fields.');
      return;
    }

    this.submitting = true;
    const dto: UpdateCustomer = {
      name: this.form.value.name || undefined,
      address: this.form.value.address || undefined,
      isSubscribed: this.form.value.isSubscribed ?? undefined,
    };
    this.customerService.updateCustomer(this.customer.id, dto).subscribe({
      next: () => {
        this.submitting = false;
        this.close();
        this.notification.success('Customer updated successfully!');
        this.saved.emit();
        this.cdr.markForCheck();
      },
      error: () => {
        this.submitting = false;
        this.cdr.markForCheck();
      },
    });
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  isFieldInvalid(field: string): boolean {
    return isFieldInvalid(this.form, field, this.submitted);
  }
}
