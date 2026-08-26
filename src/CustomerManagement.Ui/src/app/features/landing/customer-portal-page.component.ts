import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'app-customer-portal-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="landing">
      <h1>Customer Portal</h1>
      <p>Your customer-facing landing route is active. Portal internals are intentionally deferred.</p>
    </section>
  `,
  styles: [
    `
      .landing {
        max-width: 860px;
        margin: 2rem auto;
        padding: 1.2rem;
        border-radius: 16px;
        border: 1px solid #d7e4f1;
        background: #fff;
      }
    `
  ]
})
export class CustomerPortalPageComponent {}
