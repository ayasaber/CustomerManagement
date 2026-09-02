import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuickReplyResponse } from '../../../core/models/agent-dashboard.models';

@Component({
  selector: 'app-quick-reply-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="panel">
      <h3>Quick Replies</h3>
      <input
        type="search"
        [ngModel]="search()"
        (ngModelChange)="search.set($event)"
        placeholder="Search replies" />

      <ul>
        <li *ngFor="let reply of filteredReplies()">
          <div>
            <strong>{{ reply.title }}</strong>
            <small>{{ reply.tags.join(', ') }}</small>
          </div>
          <button type="button" (click)="insertClicked.emit(reply.body)">Insert</button>
        </li>
      </ul>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      h3 { margin: 0 0 0.6rem; }
      input { width: 100%; padding: 0.45rem 0.55rem; border: 1px solid #bfd2e8; border-radius: 8px; margin-bottom: 0.5rem; }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
      li { border: 1px solid #ecf1f6; border-radius: 10px; padding: 0.5rem; display: flex; justify-content: space-between; gap: 0.6rem; }
      small { display: block; color: #5d7592; }
      button { border: 1px solid #86a3c5; border-radius: 8px; background: #f6fbff; color: #1f4f84; padding: 0.4rem 0.7rem; }
    `
  ]
})
export class QuickReplyPickerComponent {
  @Input() replies: QuickReplyResponse[] = [];
  @Output() readonly insertClicked = new EventEmitter<string>();

  readonly search = signal('');

  filteredReplies(): QuickReplyResponse[] {
    const term = this.search().trim().toLowerCase();
    if (!term) {
      return this.replies;
    }

    return this.replies.filter((reply) => {
      const inTitle = reply.title.toLowerCase().includes(term);
      const inBody = reply.body.toLowerCase().includes(term);
      const inTags = reply.tags.some((tag) => tag.toLowerCase().includes(term));
      return inTitle || inBody || inTags;
    });
  }
}
