# ADR 0002 — Shell web shadcn-admin

Statut : accepté

Le frontend React/Vite dérive de `satnaing/shadcn-admin`. RiposteOS conserve le
shell, les composants shadcn/Radix et les outils TanStack, mais retire Clerk,
les données de démonstration et le faux mécanisme d'authentification. ASP.NET
Core Identity fournira l'authentification self-hosted lorsque ce cas d'usage
sera implémenté.
