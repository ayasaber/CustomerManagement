# Customer Management Gateway Route Table

Customer and ticket-management APIs are exposed via Ocelot under the `/api/customers` and `/api/tickets` upstream route surfaces.

## Downstream Service

- Host: configured per environment in `ocelot.json` (`DownstreamHostAndPorts`)
- Base path: same as upstream paths shown below

## Public Route Contract

| Upstream Path | Methods | Downstream Path | Purpose |
|---|---|---|---|
| `/api/customers` | `POST` | `/api/customers` | Create customer profile |
| `/api/customers/{customerId}` | `GET`, `PUT` | `/api/customers/{customerId}` | Get/update customer profile |
| `/api/customers/{customerId}/contact-details` | `GET` | `/api/customers/{customerId}/contact-details` | Get customer contacts |
| `/api/customers/{customerId}/notes` | `GET`, `POST` | `/api/customers/{customerId}/notes` | List/add customer notes |
| `/api/customers/{customerId}/attachments` | `GET`, `POST` | `/api/customers/{customerId}/attachments` | List/upload attachments |
| `/api/customers/{customerId}/attachments/{attachmentId}/content` | `GET` | `/api/customers/{customerId}/attachments/{attachmentId}/content` | Download attachment |
| `/api/customers/{customerId}/interaction-history` | `GET` | `/api/customers/{customerId}/interaction-history` | Read interaction history |

## Ticket Route Contract

| Upstream Path | Methods | Downstream Path | Purpose |
|---|---|---|---|
| `/api/tickets/categories` | `GET`, `POST` | `/api/tickets/categories` | List/create ticket categories |
| `/api/tickets/categories/{categoryId}` | `PUT` | `/api/tickets/categories/{categoryId}` | Update ticket category |
| `/api/tickets/priorities` | `GET`, `POST` | `/api/tickets/priorities` | List/create ticket priorities |
| `/api/tickets/priorities/{priorityId}` | `PUT` | `/api/tickets/priorities/{priorityId}` | Update ticket priority |
| `/api/tickets` | `GET`, `POST` | `/api/tickets` | List/create tickets |
| `/api/tickets/{ticketId}` | `GET`, `PUT` | `/api/tickets/{ticketId}` | Get/update ticket core fields |
| `/api/tickets/{ticketId}/status` | `PUT` | `/api/tickets/{ticketId}/status` | Transition ticket status |
| `/api/tickets/{ticketId}/assign` | `PUT` | `/api/tickets/{ticketId}/assign` | Assign ticket to another user |
| `/api/tickets/{ticketId}/self-assign` | `PUT` | `/api/tickets/{ticketId}/self-assign` | Assign ticket to current user |
| `/api/tickets/{ticketId}/escalate` | `POST` | `/api/tickets/{ticketId}/escalate` | Escalate ticket |
| `/api/tickets/{ticketId}/reopen` | `POST` | `/api/tickets/{ticketId}/reopen` | Reopen resolved/closed ticket |
| `/api/tickets/{ticketId}/history` | `GET` | `/api/tickets/{ticketId}/history` | Read ticket history |

## Auth Route Contract

| Upstream Path | Methods | Downstream Path | Auth |
|---|---|---|---|
| `/api/auth/register` | `POST` | `/api/auth/register` | No bearer token required |
| `/api/auth/login` | `POST` | `/api/auth/login` | No bearer token required |
| `/api/auth/refresh` | `POST` | `/api/auth/refresh` | No bearer token required |
| `/api/auth/logout` | `POST` | `/api/auth/logout` | Bearer token recommended |

Protected customer-management and ticket-management routes require `Authorization: Bearer <token>`.

## Identity Forwarding

The gateway forwards these headers to downstream APIs for observability context:

- `X-User-Id`
- `X-User-Email`

Client-supplied role headers are not trusted. Downstream APIs remain the source of truth for authorization decisions and validation behavior. Gateway responses preserve downstream status codes and payloads.
