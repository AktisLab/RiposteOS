# ADR 0004 — Stockage documentaire compatible S3

- Status: Accepted
- Date: 2026-07-17

## Context

Les binaires de DCE et de leurs pièces jointes ne doivent pas gonfler PostgreSQL,
ni être chargés ou sauvegardés avec les métadonnées métier. RiposteOS doit rester
self-hosted sans coupler le domaine à un serveur de stockage précis.

## Decision

Les métadonnées vivent dans PostgreSQL et les binaires privés dans un stockage objet
compatible S3. L'application dépend du contrat S3 via `IObjectStorage`, jamais de
SeaweedFS. SeaweedFS `weed mini` est l'implémentation locale self-hosted de référence.
Les téléchargements passent initialement par l'API afin de préserver le contrôle
d'accès et de ne pas exposer les objets.

## Consequences

Suppression publique, versionnement logique, URL présignées et RAG sont différés.
Une écriture échouée dans PostgreSQL après un upload déclenche seulement une
compensation S3 best-effort : il n'y a pas de transaction distribuée.
