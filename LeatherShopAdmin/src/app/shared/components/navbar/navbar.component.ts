import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { BadgeModule } from 'primeng/badge';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { Router } from '@angular/router';
import { interval } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService, OrderNotification } from '../../../core/services/signalr.service';
import { NotificationService } from '../../services/notification.service';
import { TimeAgoPipe } from '../../pipes/time.pipes';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [MenubarModule, ButtonModule, TooltipModule, BadgeModule, OverlayPanelModule, TimeAgoPipe],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent implements OnInit {
  private auth = inject(AuthService);
  private signalR = inject(SignalRService);
  private notification = inject(NotificationService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  items: MenuItem[] = [];
  username = '';
  notifications: OrderNotification[] = [];
  timeAgoTick = 0;

  ngOnInit(): void {
    this.username = this.auth.getUsername() || 'Admin';
    this.items = [
      { label: 'Dashboard', icon: 'pi pi-home', routerLink: '/dashboard', routerLinkActiveOptions: { exact: true } },
      { label: 'Products', icon: 'pi pi-box', routerLink: '/products', routerLinkActiveOptions: { exact: false } },
      { label: 'Orders', icon: 'pi pi-shopping-cart', routerLink: '/orders', routerLinkActiveOptions: { exact: true } },
      { label: 'Customers', icon: 'pi pi-users', routerLink: '/customers', routerLinkActiveOptions: { exact: true } },
      { label: 'Chat', icon: 'pi pi-comments', routerLink: '/chat', routerLinkActiveOptions: { exact: true } },
      {
        label: 'Broadcast',
        icon: 'pi pi-megaphone',
        routerLink: '/broadcast',
        routerLinkActiveOptions: { exact: true },
      },
    ];

    // Reactively start/stop SignalR based on auth state
    // Handles both fresh login AND page refresh (session restore)
    this.auth.isAuthenticated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(authenticated => {
      if (authenticated) {
        this.username = this.auth.getUsername() || 'Admin';
        this.signalR.start();
      } else {
        this.signalR.stop();
      }
    });

    this.signalR.newOrder$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(order => {
      this.notifications = [order, ...this.notifications.slice(0, 19)];
      this.notification.success(`New paid order: #${order.orderNumber} — ₹${order.amount}`);
      this.cdr.markForCheck();
    });
    this.signalR.outboxFailed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      this.notification.error(
        `Message delivery failed for ${event.customerName}: ${event.context}. Go to Chat → Failed Messages to retry.`,
      );
    });

    // Tick every 60s to refresh the timeAgo pipe's relative timestamps
    interval(60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.timeAgoTick++;
      this.cdr.markForCheck();
    });
  }

  clearNotifications(): void {
    this.notifications = [];
  }

  onNotificationClick(n: OrderNotification): void {
    this.notifications = this.notifications.filter(x => x !== n);
    this.router.navigate(['/orders']);
  }

  logout(): void {
    this.router.navigate(['/login'], { state: { fromLogout: true } }).then(navigated => {
      if (navigated) {
        // clearSession() emits isAuthenticated$=false → subscription stops SignalR
        this.auth.serverLogout();
        this.auth.clearSession();
      }
    });
  }
}
