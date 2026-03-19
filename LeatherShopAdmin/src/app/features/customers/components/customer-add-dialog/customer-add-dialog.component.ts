import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { CreateCustomer, CUSTOMER_CATEGORIES } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid } from '../../../../shared/utils/form.utils';

import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';

@Component({
  selector: 'app-customer-add-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DialogModule, InputTextModule, InputTextareaModule, ButtonModule, DropdownModule],
  templateUrl: './customer-add-dialog.component.html',
  styleUrl: './customer-add-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerAddDialogComponent {
  private fb = inject(FormBuilder);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();

  form = this.fb.group({
    phoneNumber: ['', [Validators.required, Validators.pattern(/^\d{10,15}$/)]],
    name: [''],
    address: ['', [Validators.required, Validators.minLength(10)]],
    category: [null as string | null, [Validators.required]],
  });

  categoryOptions = CUSTOMER_CATEGORIES;
  submitting = false;
  submitted = false;

  onShow(): void {
    this.submitted = false;
    this.form.reset({ phoneNumber: '', name: '', address: '', category: null });
  }

  submit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.notification.error('Please fill all required fields');
      return;
    }

    this.submitting = true;
    const dto: CreateCustomer = {
      phoneNumber: this.form.value.phoneNumber!,
      name: this.form.value.name || undefined,
      address: this.form.value.address || undefined,
      category: this.form.value.category!,
    };
    this.customerService.createCustomer(dto).subscribe({
      next: () => {
        this.submitting = false;
        this.close();
        this.notification.success('Customer added successfully!');
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
