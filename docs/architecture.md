# Architecture technique

RiposteOS démarre comme un monolithe modulaire .NET 10, déployé avec une API,
un worker et une application web React.

## Composants

- `RiposteOS.Api` expose l'API HTTP, OpenAPI et les contrôles de santé.
- `RiposteOS.Worker` exécute les traitements asynchrones avec Hangfire.
- `RiposteOS.Core` contient les modules métier sans dépendance infrastructure.
- `RiposteOS.Infrastructure` contient EF Core, PostgreSQL, Identity et les
  adaptateurs externes.
- `web` contient l'application React/Vite basée sur shadcn-admin.

Une seule base PostgreSQL stocke les données métier, Identity, Hangfire et les
vecteurs pgvector. Les documents binaires resteront hors base.

## Règles

- Organisation par module métier puis par cas d'usage.
- EF Core est l'unité de travail ; pas de repository générique.
- Interfaces uniquement aux frontières externes réelles.
- Appels asynchrones idempotents et état métier conservé hors Hangfire.
- Frontend et API sous la même origine en production.
- TanStack Query gère l'état serveur ; React gère l'état local.

Les décisions détaillées sont consignées dans `docs/adr`.
