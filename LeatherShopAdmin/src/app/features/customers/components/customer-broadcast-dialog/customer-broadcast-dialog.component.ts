import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, OnInit, inject } from '@angular/core';

import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { BroadcastFormHelperService } from '../../../broadcast/services/broadcast-form-helper.service';
import { CarouselCard } from '../../../broadcast/models/broadcast.model';
import { ProductImageItem } from '../../../products/models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid as checkFieldInvalid } from '../../../../shared/utils/form.utils';

import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-broadcast-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, DialogModule, DropdownModule, InputTextModule, ButtonModule],
  templateUrl: './customer-broadcast-dialog.component.html',
  styleUrl: './customer-broadcast-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [BroadcastFormHelperService],
})
export class CustomerBroadcastDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);
  readonly helper = inject(BroadcastFormHelperService);

  @Input() phoneNumbers: string[] = [];
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() sent = new EventEmitter<void>();

  broadcastForm!: FormGroup;
  sending = false;
  submitted = false;

  ngOnInit(): void {
    this.broadcastForm = this.fb.group({
      template: ['', [Validators.required, this.templateValidator.bind(this)]],
      params: [''],
      imageUrl: [''],
    });
    this.helper.init();
  }

  // ─── Template Selection ───

  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    return this.helper.isValidTemplate(value) ? null : { invalidTemplate: true };
  }

  get isValidTemplate(): boolean {
    return this.helper.isValidTemplate(this.broadcastForm.get('template')?.value);
  }

  onTemplateSelect(): void {
    const name = this.broadcastForm.get('template')?.value;
    this.broadcastForm.get('template')?.updateValueAndValidity();
    this.helper.applyTemplate(name);
    if (!this.helper.hasImageHeader) {
      this.broadcastForm.patchValue({ imageUrl: '' });
    }
  }

  // ─── Header Image ───

  onHeaderImageSelect(event: Event): void {
    this.helper.handleHeaderImageUpload(event, path => {
      this.broadcastForm.patchValue({ imageUrl: path });
    });
  }

  removeHeaderImage(): void {
    this.helper.clearHeaderImage();
    this.broadcastForm.patchValue({ imageUrl: '' });
  }

  // ─── Linked Product (standard template) ───

  onLinkedProductSelect(): void {
    this.helper.onLinkedProductSelect(
      params => this.broadcastForm.patchValue({ params }),
      path => this.broadcastForm.patchValue({ imageUrl: path })
    );
    this.cdr.markForCheck();
  }

  onLinkedImageSelect(img: ProductImageItem): void {
    this.helper.selectLinkedProductImage(img, path => {
      this.broadcastForm.patchValue({ imageUrl: path });
    });
    this.cdr.markForCheck();
  }

  // ─── Computed ───

  isFieldInvalid(field: string): boolean {
    return checkFieldInvalid(this.broadcastForm, field, this.submitted);
  }

  // ─── Send ───

  send(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.notification.error('Please select a valid approved template!');
      return;
    }

    if (this.helper.isCarousel && !this.helper.carouselCardsValid) {
      this.notification.error('Please select images for all carousel cards.');
      return;
    }

    if (this.phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }

    this.sending = true;
    const templateName = this.broadcastForm.get('template')?.value;
    const languageCode = this.helper.getLanguageCode(templateName);

    if (this.helper.isCarousel) {
      const cards: CarouselCard[] = this.helper.carouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload,
      }));
      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: [],
          isCarousel: true,
          carouselCards: cards,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(`Carousel sending to ${res.totalRecipients} customer${res.totalRecipients === 1 ? '' : 's'}...`);
          },
          error: () => {
            this.sending = false;
            this.cdr.markForCheck();
          },
        });
    } else {
      const rawParams = this.broadcastForm.get('params')?.value || '';
      // Use pre-split array when product is linked (descriptions may contain commas)
      const params = this.helper.linkedProductParams.length > 0
        ? this.helper.linkedProductParams
        : (rawParams.trim() ? rawParams.split(',').map((p: string) => p.trim()) : []);
      const imageUrl = this.broadcastForm.get('imageUrl')?.value;

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: params,
          imageUrl: imageUrl || undefined,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(`Broadcast sending to ${res.totalRecipients} customer${res.totalRecipients === 1 ? '' : 's'}...`);
          },
          error: () => {
            this.sending = false;
            this.cdr.markForCheck();
          },
        });
    }
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.submitted = false;
    this.broadcastForm.reset({ template: '', params: '', imageUrl: '' });
    this.helper.reset();
  }

  // ─── Private ───

  private onSendSuccess(message: string): void {
    this.sending = false;
    this.close();
    this.notification.success(message);
    this.sent.emit();
  }
}
