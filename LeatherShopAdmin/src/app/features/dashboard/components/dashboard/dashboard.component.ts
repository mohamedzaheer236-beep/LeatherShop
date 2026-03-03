import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { Dashboard } from '../../models/dashboard.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { getStatusSeverity, TagSeverity } from '../../../../shared/utils/severity.utils';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    LoadingSpinnerComponent,
    CardModule,
    TableModule,
    TagModule,
    ButtonModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private cdr = inject(ChangeDetectorRef);

  dashboard: Dashboard | null = null;
  loading = true;
  errorMessage: string | null = null;

  ngOnInit(): void {
    this.loadDashboard();
  }

  retry(): void {
    this.loading = true;
    this.errorMessage = null;
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.dashboardService.getDashboard().subscribe({
      next: data => {
        this.dashboard = data;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Failed to load dashboard data. Please try again.';
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }
}
