import { routes } from './app.routes';
import { agentOnlyGuard } from './core/guards/agent-only.guard';

describe('App routes', () => {
  it('protects /agent/dashboard with agent-capable guard', () => {
    const route = routes.find((candidate) => candidate.path === 'agent/dashboard');
    expect(route).toBeTruthy();
    expect(route?.canActivate).toBeTruthy();
    expect(route?.canActivate?.includes(agentOnlyGuard)).toBeTrue();
  });
});
