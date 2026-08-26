import { Component } from '@angular/core';

@Component({
  selector: 'app-forbidden-page',
  standalone: true,
  template: `
    <main style="padding:2rem;max-width:680px;margin:0 auto;font-family:Segoe UI,Tahoma,Geneva,Verdana,sans-serif;">
      <h1>Agent role required</h1>
      <p>The customer management workspace is restricted to users with the agent role.</p>
      <p>Switch role to agent in local storage key <strong>customer-ui-role</strong> or from the role control on the page.</p>
    </main>
  `
})
export class ForbiddenPageComponent {}
