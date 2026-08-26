import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-agent-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="landing">
      <h1>Agent Dashboard</h1>
      <p>This landing route is ready. Agent workspace internals remain out of scope for Story 12.</p>
      <div class="actions">
        <a routerLink="/customers">Open Customer Workspace</a>
      </div>
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

      .actions a {
        color: #0f5bad;
        font-weight: 700;
      }
    `
  ]
})
export class AgentDashboardPageComponent {}
