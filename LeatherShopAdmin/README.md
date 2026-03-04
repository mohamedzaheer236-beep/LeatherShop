# LeatherShopAdmin

Leather Shop Admin Portal — Angular 18 admin dashboard for managing products, orders, customers, broadcasts, and chat.

## Features

- **Dashboard** — Analytics and overview
- **Products** — CRUD product management
- **Orders** — Order tracking and management
- **Customers** — Customer management
- **Broadcast** — WhatsApp broadcast messaging
- **Chat** — Real-time chat with customers
- **Auth** — Secure admin login with JWT

## Login Page

The login page features an interactive **Spline 3D background** with a glassmorphism login card.

### Technical Details
- **3D Background**: Hosted on [Spline](https://spline.design) via public embed URL (iframe)
- **Glassmorphism Card**: Semi-transparent card with `backdrop-filter: blur()` and purple-blue gradient
- **Lazy Loading**: The 3D scene loads after the login form appears for better perceived performance
- **Fallback**: If Spline CDN is unavailable, the page shows a dark background — login form remains fully functional
- **Files Modified**: `login.component.html`, `login.component.scss` only — no TS logic or dependencies changed

### Spline Scene Dependency
The login background relies on a published Spline scene:
```
https://my.spline.design/interactiveaistartupheropage-s4MZKTFkESyL5jVYbgOLMYYB/
```
> **Note**: Do not unpublish or delete this scene from the Spline account, or the 3D background will stop loading.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
