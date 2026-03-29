import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
  ElementRef,
  NgZone,
} from '@angular/core';
import { DatePipe, DecimalPipe, UpperCasePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { Dashboard } from '../../models/dashboard.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { getStatusSeverity, TagSeverity } from '../../../../shared/utils/severity.utils';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    UpperCasePipe,
    RouterLink,
    LoadingSpinnerComponent,
    CardModule,
    TableModule,
    TagModule,
    ButtonModule,
    ChartModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private cdr = inject(ChangeDetectorRef);
  private el = inject(ElementRef);
  private zone = inject(NgZone);

  dashboard: Dashboard | null = null;
  loading = true;
  errorMessage: string | null = null;

  // Animated display values
  animatedValues: Record<string, number> = {};

  // Chart data
  revenueChartData: any;
  revenueChartOptions: any;
  statusChartData: any;
  statusChartOptions: any;

  // Current greeting
  greeting = '';
  currentDate = new Date();

  ngOnInit(): void {
    this.setGreeting();
    this.loadDashboard();
  }

  retry(): void {
    this.loading = true;
    this.errorMessage = null;
    this.loadDashboard();
  }

  private setGreeting(): void {
    const h = new Date().getHours();
    this.greeting = h < 12 ? 'Good Morning' : h < 17 ? 'Good Afternoon' : 'Good Evening';
  }

  private loadDashboard(): void {
    this.dashboardService.getDashboard().subscribe({
      next: data => {
        this.dashboard = data;
        this.loading = false;
        this.buildRevenueChart(data);
        this.buildStatusChart(data);
        this.cdr.markForCheck();

        // Trigger animations after Angular renders the new DOM
        setTimeout(() => {
          this.observeCards();
          this.startCountUp();
        }, 50);
      },
      error: () => {
        this.errorMessage = 'Failed to load dashboard data. Please try again.';
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private buildRevenueChart(data: Dashboard): void {
    const labels = data.monthlyRevenue.map(m => m.label);
    const revenues = data.monthlyRevenue.map(m => m.revenue);
    const orders = data.monthlyRevenue.map(m => m.orderCount);

    this.revenueChartData = {
      labels,
      datasets: [
        {
          type: 'bar',
          label: 'Revenue (₹)',
          data: revenues,
          backgroundColor: 'rgba(99, 102, 241, 0.2)',
          borderColor: '#6366f1',
          borderWidth: 2,
          borderRadius: 6,
          yAxisID: 'y',
          order: 2,
        },
        {
          type: 'line',
          label: 'Orders',
          data: orders,
          borderColor: '#e0c097',
          backgroundColor: 'rgba(224, 192, 151, 0.15)',
          fill: true,
          tension: 0.4,
          pointRadius: 4,
          pointHoverRadius: 6,
          pointBackgroundColor: '#e0c097',
          yAxisID: 'y1',
          order: 1,
        },
      ],
    };

    this.revenueChartOptions = {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: {
          position: 'top',
          labels: { usePointStyle: true, padding: 16, font: { family: 'Inter', size: 12 } },
        },
        tooltip: {
          backgroundColor: '#1a1a2e',
          titleFont: { family: 'Inter', size: 13 },
          bodyFont: { family: 'Inter', size: 12 },
          padding: 12,
          cornerRadius: 8,
          callbacks: {
            label: (ctx: any) => {
              if (ctx.dataset.yAxisID === 'y') {
                return `Revenue: ₹${ctx.parsed.y.toLocaleString('en-IN')}`;
              }
              return `Orders: ${ctx.parsed.y}`;
            },
          },
        },
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { font: { family: 'Inter', size: 11 }, color: '#94a3b8' },
        },
        y: {
          position: 'left',
          grid: { color: '#f0f0f0' },
          ticks: {
            font: { family: 'Inter', size: 11 },
            color: '#94a3b8',
            callback: (v: number) => (v >= 1000 ? `₹${(v / 1000).toFixed(0)}k` : `₹${v}`),
          },
        },
        y1: {
          position: 'right',
          grid: { drawOnChartArea: false },
          ticks: {
            font: { family: 'Inter', size: 11 },
            color: '#94a3b8',
            stepSize: 1,
          },
        },
      },
    };
  }

  private buildStatusChart(data: Dashboard): void {
    const statusColors: Record<string, string> = {
      Pending: '#f59e0b',
      Confirmed: '#3b82f6',
      Shipped: '#8b5cf6',
      Delivered: '#10b981',
      Cancelled: '#ef4444',
    };

    const labels = data.ordersByStatus.map(s => s.status);
    const counts = data.ordersByStatus.map(s => s.count);
    const colors = labels.map(l => statusColors[l] || '#94a3b8');

    this.statusChartData = {
      labels,
      datasets: [
        {
          data: counts,
          backgroundColor: colors,
          borderWidth: 0,
          hoverOffset: 8,
        },
      ],
    };

    this.statusChartOptions = {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '65%',
      plugins: {
        legend: {
          position: 'bottom',
          labels: { usePointStyle: true, padding: 16, font: { family: 'Inter', size: 12 } },
        },
        tooltip: {
          backgroundColor: '#1a1a2e',
          titleFont: { family: 'Inter', size: 13 },
          bodyFont: { family: 'Inter', size: 12 },
          padding: 12,
          cornerRadius: 8,
        },
      },
    };
  }

  private startCountUp(): void {
    if (!this.dashboard) return;
    const targets: Record<string, number> = {
      products: this.dashboard.totalProducts,
      customers: this.dashboard.totalCustomers,
      orders: this.dashboard.totalOrders,
      revenue: this.dashboard.totalRevenue,
      pending: this.dashboard.pendingOrders,
      lowStock: this.dashboard.lowStockProducts,
    };

    Object.keys(targets).forEach(key => (this.animatedValues[key] = 0));
    this.cdr.markForCheck();

    this.zone.runOutsideAngular(() => {
      const duration = 800;
      const start = performance.now();
      const step = (now: number) => {
        const progress = Math.min((now - start) / duration, 1);
        const ease = 1 - Math.pow(1 - progress, 3); // ease-out cubic
        Object.keys(targets).forEach(key => {
          this.animatedValues[key] = Math.round(targets[key] * ease);
        });
        this.zone.run(() => this.cdr.markForCheck());
        if (progress < 1) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
    });
  }

  private observeCards(): void {
    if (typeof IntersectionObserver === 'undefined') return;
    const cards = this.el.nativeElement.querySelectorAll('.animate-in');
    const observer = new IntersectionObserver(
      entries => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            (entry.target as HTMLElement).classList.add('visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.1 }
    );
    cards.forEach((card: Element) => observer.observe(card));
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }

  getGrowthIcon(value: number): string {
    return value >= 0 ? 'pi pi-arrow-up' : 'pi pi-arrow-down';
  }

  getGrowthClass(value: number): string {
    return value >= 0 ? 'growth-positive' : 'growth-negative';
  }
}
