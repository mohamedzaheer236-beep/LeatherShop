import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, MenubarModule, ButtonModule, TooltipModule],
  template: `
    <p-menubar [model]="items" styleClass="navbar-menubar">
      <ng-template pTemplate="start">
        <span class="navbar-brand">
          <i class="pi pi-briefcase navbar-logo"></i>
          <span class="navbar-brand-text">Leather Shop Admin</span>
        </span>
      </ng-template>
      <ng-template pTemplate="end">
        <div class="navbar-end">
          <div class="navbar-user-badge">
            <i class="pi pi-user"></i>
            <span>{{ username }}</span>
          </div>
          <div class="navbar-divider"></div>
          <button pButton icon="pi pi-power-off"
                  class="p-button-rounded p-button-sm navbar-logout-btn"
                  pTooltip="Logout" tooltipPosition="bottom"
                  (click)="logout()"></button>
        </div>
      </ng-template>
    </p-menubar>
  `,
  styles: [`
    .navbar-end {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-left: auto;
    }
    .navbar-user-badge {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      background: rgba(255, 255, 255, 0.1);
      padding: 0.35rem 0.75rem;
      border-radius: 20px;
      font-size: 0.82rem;
      color: rgba(255, 255, 255, 0.85);
      font-weight: 500;
      letter-spacing: 0.3px;
    }
    .navbar-user-badge i {
      font-size: 0.8rem;
      color: var(--ls-brand-gold, #c8a951);
    }
    .navbar-divider {
      width: 1px;
      height: 24px;
      background: rgba(255, 255, 255, 0.15);
    }
    .navbar-logout-btn {
      background: rgba(220, 38, 38, 0.15) !important;
      border: 1px solid rgba(220, 38, 38, 0.3) !important;
      color: #fca5a5 !important;
      width: 34px !important;
      height: 34px !important;
      transition: all 0.2s ease !important;
    }
    .navbar-logout-btn:hover {
      background: rgba(220, 38, 38, 0.35) !important;
      border-color: rgba(220, 38, 38, 0.5) !important;
      color: #fff !important;
    }
  `]
})
export class NavbarComponent implements OnInit {
  items: MenuItem[] = [];
  username = '';

  constructor(private auth: AuthService) {}

  ngOnInit(): void {
    this.username = this.auth.getUsername() || 'Admin';
    this.items = [
      { label: 'Dashboard', icon: 'pi pi-home', routerLink: '/dashboard' },
      { label: 'Products', icon: 'pi pi-box', routerLink: '/products' },
      { label: 'Orders', icon: 'pi pi-shopping-cart', routerLink: '/orders' },
      { label: 'Customers', icon: 'pi pi-users', routerLink: '/customers' },
      { label: 'Broadcast', icon: 'pi pi-megaphone', routerLink: '/broadcast' }
    ];
  }

  logout(): void {
    this.auth.logout();
  }
}