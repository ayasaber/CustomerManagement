import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AgentContextService {
  private readonly roleKey = 'customer-ui-role';
  private readonly userIdKey = 'customer-ui-user-id';

  readonly role = signal(localStorage.getItem(this.roleKey) ?? 'agent');
  readonly userId = signal(localStorage.getItem(this.userIdKey) ?? 'agent-001');

  setRole(role: string): void {
    localStorage.setItem(this.roleKey, role);
    this.role.set(role);
  }

  setUserId(userId: string): void {
    localStorage.setItem(this.userIdKey, userId);
    this.userId.set(userId);
  }
}
